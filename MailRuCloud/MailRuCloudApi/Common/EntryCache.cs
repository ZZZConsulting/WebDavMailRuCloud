using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base;
using YaR.Clouds.Links;
using YaR.Clouds.Base.Requests.Types;
using System.Linq;
using static YaR.Clouds.Extensions.Extensions;

namespace YaR.Clouds.Common;

public class EntryCache : IDisposable
{
    public enum GetState
    {
        /// <summary>
        /// Когда из менеджера кеша запрашивается файл, файла нет в кеше,
        /// а менеджер не обладает информацией о наличии или отсутствии файла в облаке.
        /// </summary>
        Unknown,
        /// <summary>
        /// Когда менеджер кеша имеет в кеше всю папку, где должен быть файл,
        /// но файла такого в папке в кеше нету, то его нет и в облаке.
        /// </summary>
        NotExists,
        /// <summary>
        /// Когда менеджер имеет в кеше файл и возвращает его.
        /// </summary>
        Entry,
        /// <summary>
        /// Когда менеджер имеет в кеше папку, но в кеше отсутствует ее содержимое, которое надо считать с сервера.
        /// </summary>
        EntryWithUnknownContent
    };

    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(EntryCache));

    //private static readonly TimeSpan _minCleanUpInterval = new TimeSpan(0, 0, 10 /* секунды */ );
    //private static readonly TimeSpan _maxCleanUpInterval = new TimeSpan(0, 10 /* минуты */, 0);

    // По умолчанию очистка кеша от устаревших записей производится каждые 30 секунд
    private readonly TimeSpan _cleanUpPeriod = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _expirePeriod;

    public bool IsCacheEnabled { get; private set; }

    private readonly System.Timers.Timer _cleanTimer;

    private readonly ConcurrentDictionary<string /* full path */, CacheItem> _root =
        new(StringComparer.InvariantCultureIgnoreCase);

    private readonly SemaphoreSlim _rootLocker = new SemaphoreSlim(1);

    public delegate Task<CheckUpInfo> CheckOperations();
    private readonly CheckOperations _activeOperationsAsync;
    private readonly System.Timers.Timer _checkActiveOperationsTimer;
    // Проверка активных операций на сервере и наличия внешних изменений в облаке мимо сервиса
    private readonly TimeSpan _opCheckPeriod = TimeSpan.FromSeconds(15);
    /// <summary>
    /// Сохраняемая с сервера и пополняемая при операциях данного сервиса информация о состоянии Диска,
    /// для выявление внешних операций на Диске, минуя данный сервис, чтобы вовремя
    /// сбросить кеш и обновить информацию с сервера.
    /// </summary>
    private CheckUpInfo.CheckInfo _lastComparedInfo;
    /// <summary>
    /// Блокировщик доступа к <see cref="_lastComparedInfo"/>
    /// </summary>
    private readonly SemaphoreSlim _lastComparedInfoLocker = new SemaphoreSlim(1);



    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal class CacheItem
    {
        /// <summary>
        /// Отметка о времени размещения в кеше.
        /// </summary>
        public DateTime CreationTime { get; set; }
        /// <summary>
        /// Entry - файл, папка или link в кеше, если не null.
        /// Или null, если сохраняется информация о том,
        /// что на сервере нет такого файла или папки, а то есть некоторые клиенты,
        /// которые многократно требуют то, что было уже ранее удалено.
        /// </summary>
        public IEntry Entry { get; set; }

        /// <summary>
        /// Если часть файлов кеша папки устарело и отсутствует,
        /// нельзя выдавать содержимое папки из кеша.
        /// </summary>
        public bool AllDescendantsInCache { get; set; }

        public string DebuggerDisplay =>
            $"{(Entry is null
                ? "<<DELETED on server>>"
                : Entry is Link
                    ? "<<Link>>"
                    : Entry is File
                        ? "<<File>>"
                        : AllDescendantsInCache
                            ? "<<FOLDER with DESCENDANTS>>"
                            : "<<JUST folder entry>>")}"
            + $", Since {CreationTime:HH:mm:ss} {Entry?.FullPath}";
    }

    /// <summary>
    /// Перед выполнение любой операции сюда в мешок помещается путь к файлу или папке.
    /// После окончания выполнения операции, пусть файла или папки удаляется отсюда из мешка.
    /// По наличию пути в этом мешке в методе CheckActiveOps проверяется чья операция
    /// происходит на сервере - данного сервиса или сторонняя, и если стороннего,
    /// то кеш сбрасывается.
    /// </summary>
    private readonly Dictionary<string /* full path */, CounterClass> _registeredOperationPath =
        new(StringComparer.InvariantCultureIgnoreCase);
    /// <summary>
    /// Блокировщик доступа к <see cref="_registeredOperationPath"/>>
    /// </summary>
    private readonly SemaphoreSlim _operationLocker = new SemaphoreSlim(1);

    internal class /* это должен быть class, не struct */ CounterClass
    {
        public int Value;
    }

    public EntryCache(TimeSpan expirePeriod, CheckOperations activeOperationsAsync)
    {
        _expirePeriod = expirePeriod;
        IsCacheEnabled = Math.Abs(_expirePeriod.TotalMilliseconds) > 0.01;

        _lastComparedInfo = null;
        _activeOperationsAsync = activeOperationsAsync;
        _checkActiveOperationsTimer = null;
        _cleanTimer = null;

        if (IsCacheEnabled)
        {
            _cleanTimer = new System.Timers.Timer()
            {
                Interval = _cleanUpPeriod.TotalMilliseconds,
                Enabled = true,
                AutoReset = true
            };
            _cleanTimer.Elapsed += RemoveExpired;

            if (_activeOperationsAsync is not null && expirePeriod.TotalMinutes >= 1)
            {
                // Если кеш достаточно длительный, делаются регулярные
                // проверки на изменения в облаке
                _checkActiveOperationsTimer = new System.Timers.Timer()
                {
                    Interval = _opCheckPeriod.TotalMilliseconds,
                    Enabled = true,
                    AutoReset = true
                };
                _checkActiveOperationsTimer.Elapsed += CheckActiveOpsAsync;

                // Для инициализации коллекции счетчиков, обращение к серверу за актуальными значениями
                CheckActiveOpsAsync();
            }
        }
    }

    #region IDisposable Support
    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            if (_checkActiveOperationsTimer is not null)
            {
                _checkActiveOperationsTimer.Stop();
                _checkActiveOperationsTimer.Enabled = false;
                _checkActiveOperationsTimer.Dispose();
            }
            if (_cleanTimer is not null)
            {
                _cleanTimer.Enabled = false;
                _cleanTimer?.Stop();
                _cleanTimer?.Dispose();
            }
            ClearNoLock();
            _rootLocker?.Dispose();
            _operationLocker?.Dispose();

            Logger.Debug("EntryCache is disposed");
        }
        _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(true);
    }
    #endregion

    //public TimeSpan CleanUpPeriod
    //{
    //    get => _cleanUpPeriod;
    //    set
    //    {
    //        // Очистку кеша от устаревших записей не следует проводить часто чтобы не нагружать машину,
    //        // и не следует проводить редко, редко, чтобы не натыкаться постоянно на устаревшие записи.
    //        _cleanUpPeriod = value < _minCleanUpInterval
    //                         ? _minCleanUpInterval
    //                         : value > _maxCleanUpInterval
    //                           ? _maxCleanUpInterval
    //                           : value;

    //        if (IsCacheEnabled)
    //        {
    //            long cleanPreiod = (long)_cleanUpPeriod.TotalMilliseconds;
    //            _cleanTimer.Change(cleanPreiod, cleanPreiod);
    //        }
    //    }
    //}

    private void RemoveExpired(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (_root.IsEmpty)
            return;

        var watch = Stopwatch.StartNew();

        DateTime threshold = DateTime.Now - _expirePeriod;
        int removedCount = 0;
        int partiallyExpiredCount = 0;
        foreach (var entry in _root)
        {
            _rootLocker.LockedAction(() =>
            {
                if (entry.Value.CreationTime <= threshold &&
                    _root.TryRemove(entry.Key, out var cacheEntry))
                {
                    /*
                     * Если элемент кеша устарел и удаляется, то
                     * у папки, в которой он содержится, если она в кеше,
                     * ставится признак AllChildrenInCache=false,
                     * чтобы при запросе на выборку содержимого папки
                     * не был возвращен неполный список из-за удаленных
                     * из кеша устаревших элементов.
                     */
                    removedCount++;
                    if (entry.Key != WebDavPath.Root)
                    {
                        if (_root.TryGetValue(WebDavPath.Parent(entry.Key), out var parentEntry) &&
                            parentEntry.Entry is Folder)
                        {
                            parentEntry.AllDescendantsInCache = false;
                            partiallyExpiredCount++;
                        }
                    }
                }
            });
        }

        if (removedCount > 0)
            Logger.Debug($"Items cache clean: removed {removedCount} expired " +
                $"items, {partiallyExpiredCount} marked partially expired ({watch.ElapsedMilliseconds} ms)");
    }

    public void RegisterOperation(string path, CounterOperation operation)
    {
        if (!IsCacheEnabled)
            return;

        // Регистрация пути файла или папки, над которым производится манипуляция
        _operationLocker.LockedAction(() =>
        {
            if (_registeredOperationPath.TryGetValue(path, out var counter))
            {
                counter.Value++;
            }
            else
            {
                counter = new();
                counter.Value = 1;
                _registeredOperationPath.Add(path, counter);
            }
        });

        // Увеличение счетчика, соответствующего операции
        if (operation != CounterOperation.None && IsCacheEnabled && !_root.IsEmpty)
        {
            _lastComparedInfoLocker.LockedAction(() => _lastComparedInfo?.JournalCounters.Increment(operation));
        }
    }

    public void UnregisterOperation(string path)
    {
        if (!IsCacheEnabled)
            return;

        _operationLocker.LockedAction(() =>
        {
            if (_registeredOperationPath.TryGetValue(path, out var counter) &&
                --counter.Value <= 0)
            {
                _registeredOperationPath.Remove(path);
            }
        });
    }

    private void CheckActiveOpsAsync(object sender, System.Timers.ElapsedEventArgs e)
    {
        CheckActiveOpsAsync();
    }

    private DateTime _lastCheck = DateTime.MinValue;

    private async void CheckActiveOpsAsync()
    {
        if (_disposedValue || !IsCacheEnabled || _activeOperationsAsync is null)
            return;

        if (_root.IsEmpty)
        {
            /*
             * Когда кеш пустой, периодически все равно делаем сравнение счетчиков сохраненных
             * и увеличенных на количество операций, сделанных, данным сервисом,
             * со значениями с сервера для выявления ситуации, когда на сервере значение меньше,
             * то есть не "догнало" увеличение сохраненного счетчика.
             */
            if (DateTime.Now.Subtract(_lastCheck).Minutes < 2)
                return;
        }
        _lastCheck = DateTime.Now;

        CheckUpInfo info = await _activeOperationsAsync();
        if (info is null)
            return;

        /*
         * Если на Диске производятся операции, которые идут мимо текущего сервиса,
         * с большой вероятностью, кеш будет неактуальным.
         * Наличие внешних операций отслеживается двумя методами:
         *   - сравнение счетчиков - если количество операций на Диске
         *          с учетом операций данного сервиса изменилось,
         *          значит кто-то что-то делает параллельно,
         *          минуя текущий сервис, тогда делается сброс всего кеша;
         *   - проверка активных операций на Диске (например длительно перемещение
         *          папки из одного места в другое),
         *          если такие длительные операции не изменяют счетчики,
         *          то их не отследить иначе, при обнаружении длительной операции
         *          делается сброс кеша с путем, затронутым операцией
         *          (если не менялись счетчики, если менялись - полный сброс кеша).
         *
         * Полученные и сохраненные счетчики увеличиваются данным сервисом при операциях с Диском.
         * Т.к. на сервере обновление счетчиков требует 10-20 секунд, может получиться ситуация,
         * когда от сервера пришли значения меньшие, чем сохранные (из-за увеличения при операциях данным сервисом).
         * Но через какое-то время значения от сервера должны "догнать" сохраненные значения,
         * то есть стать равными им. Если при отсутствии операций с Диском через данный сервис, значения с сервера
         * так и не догнали сохраненные значения, значит алгоритм что-то не учитывает.
         * Если значения с сервера обогнали сохраненные значения, значит на сервере что-то было сделано,
         * в обход данного сервиса, в таком случае делается сброс кеша.
         * Из-за необходимости "догонять" значения, сохраняются значения от сервера не напрямую,
         * а берется максимум из пришедших и сохраненных значений.
         */
        CheckUpInfo.CheckInfo previous = null;
        _lastComparedInfoLocker.LockedAction(() =>
        {
            if (info.AccountInfo is not null && _lastComparedInfo is not null)
            {
                info.AccountInfo.JournalCounters.TakeMax(_lastComparedInfo.JournalCounters);
            }
            previous = Interlocked.Exchange(ref _lastComparedInfo, info.AccountInfo);
        });

        // Сравнение счетчиков
        if (previous is not null)
        {
            List<string> texts = [];
            var a = previous.JournalCounters;
            var b = info.AccountInfo.JournalCounters;

            if (a.RemoveCounter < b.RemoveCounter)
                texts.Add($"{JournalCounters.RemoveCounterStr}: {a.RemoveCounter}->{b.RemoveCounter}");

            if (a.RenameCounter < b.RenameCounter)
                texts.Add($"{JournalCounters.RenameCounterStr}: {a.RenameCounter}->{b.RenameCounter}");

            if (a.MoveCounter < b.MoveCounter)
                texts.Add($"{JournalCounters.MoveCounterStr}: {a.MoveCounter}->{b.MoveCounter}");

            if (a.CopyCounter < b.CopyCounter)
                texts.Add($"{JournalCounters.CopyCounterStr}: {a.CopyCounter}->{b.CopyCounter}");

            //if (a.UpdateCounter < b.UpdateCounter)
            //    texts.Add($"{JournalCounters.UpdateCounterStr}: {a.UpdateCounter}->{b.UpdateCounter}");

            if (a.UploadCounter < b.UploadCounter)
                texts.Add($"{JournalCounters.UploadCounterStr}: {a.UploadCounter}->{b.UploadCounter}");

            if (a.TakeSomeonesFolderCounter < b.TakeSomeonesFolderCounter)
                texts.Add($"{JournalCounters.TakeSomeonesFolderCounterStr}: {a.TakeSomeonesFolderCounter}->{b.TakeSomeonesFolderCounter}");

            if (a.NewFolderCounter < b.NewFolderCounter)
                texts.Add($"{JournalCounters.NewFolderCounterStr}: {a.NewFolderCounter}->{b.NewFolderCounter}");

            if (a.RemoveToTrashCounter < b.RemoveToTrashCounter)
                texts.Add($"{JournalCounters.RemoveToTrashCounterStr}: {a.RemoveToTrashCounter}->{b.RemoveToTrashCounter}");

            if (a.RestoreFromTrashCounter < b.RestoreFromTrashCounter)
                texts.Add($"{JournalCounters.RestoreFromTrashCounterStr}: {a.RestoreFromTrashCounter}->{b.RestoreFromTrashCounter}");

            if (a.TrashDropItemCounter < b.TrashDropItemCounter)
                texts.Add($"{JournalCounters.TrashDropItemCounterStr}: {a.TrashDropItemCounter}->{b.TrashDropItemCounter}");

            if (a.TrashDropAllCounter < b.TrashDropAllCounter)
                texts.Add($"{JournalCounters.TrashDropAllCounterStr}: {a.TrashDropAllCounter}->{b.TrashDropAllCounter}");

            if (texts.Count > 0)
            {
                // Обнаружено внешнее изменении на Диске
                Logger.Warn($"External activity is detected ({string.Join(", ", texts)}). " +
                    $"If you want to keep cache you have to use the same password for your account in all your programs, " +
                    $"so please check it.");
                // Если между проверками что-то изменились, делаем полный сброс кеша
                Clear();
                return;
            }

            if (_root.IsEmpty)
            {
                // Реализуем механизм защиты, на случай, если счетчики серверные так и не догнали увеличение счетчиков локальных
                texts.Clear();

                if (a.RemoveCounter > b.RemoveCounter)
                    texts.Add($"{JournalCounters.RemoveCounterStr}: {a.RemoveCounter}->{b.RemoveCounter}");

                if (a.RenameCounter > b.RenameCounter)
                    texts.Add($"{JournalCounters.RenameCounterStr}: {a.RenameCounter}->{b.RenameCounter}");

                if (a.MoveCounter > b.MoveCounter)
                    texts.Add($"{JournalCounters.MoveCounterStr}: {a.MoveCounter}->{b.MoveCounter}");

                if (a.CopyCounter > b.CopyCounter)
                    texts.Add($"{JournalCounters.CopyCounterStr}: {a.CopyCounter}->{b.CopyCounter}");

                if (a.UploadCounter > b.UploadCounter)
                    texts.Add($"{JournalCounters.UploadCounterStr}: {a.UploadCounter}->{b.UploadCounter}");

                if (a.TakeSomeonesFolderCounter > b.TakeSomeonesFolderCounter)
                    texts.Add($"{JournalCounters.TakeSomeonesFolderCounterStr}: {a.TakeSomeonesFolderCounter}->{b.TakeSomeonesFolderCounter}");

                if (a.NewFolderCounter > b.NewFolderCounter)
                    texts.Add($"{JournalCounters.NewFolderCounterStr}: {a.NewFolderCounter}->{b.NewFolderCounter}");

                //if (a.UpdateCounter > b.UpdateCounter)
                //    texts.Add($"{JournalCounters.UpdateCounterStr}: {a.UpdateCounter}->{b.UpdateCounter}");

                if (a.RemoveToTrashCounter > b.RemoveToTrashCounter)
                    texts.Add($"{JournalCounters.RemoveToTrashCounterStr}: {a.RemoveToTrashCounter}->{b.RemoveToTrashCounter}");

                if (a.RestoreFromTrashCounter > b.RestoreFromTrashCounter)
                    texts.Add($"{JournalCounters.RestoreFromTrashCounterStr}: {a.RestoreFromTrashCounter}->{b.RestoreFromTrashCounter}");

                if (a.TrashDropItemCounter > b.TrashDropItemCounter)
                    texts.Add($"{JournalCounters.TrashDropItemCounterStr}: {a.TrashDropItemCounter}->{b.TrashDropItemCounter}");

                if (a.TrashDropAllCounter > b.TrashDropAllCounter)
                    texts.Add($"{JournalCounters.TrashDropAllCounterStr}: {a.TrashDropAllCounter}->{b.TrashDropAllCounter}");

                if (texts.Count > 0)
                {
                    // Обнаружено внешнее изменении на Диске
                    Logger.Error($"There is a problem in external activity detection algorithm ({string.Join(", ", texts)})");
                }

                _lastComparedInfoLocker.LockedAction(() =>
                {
                    // Локальные счетчики выравниваются по серверным, без увеличения на количество операций данного сервиса
                    _lastComparedInfo = info.AccountInfo;

                    // Выравнивание счетчиков сделано, повторно делать не нужно
                    _lastCheck = DateTime.MaxValue;
                });
            }

            //if (info.AccountInfo.FilesCount != previous.Value.FilesCount ||
            //    info.AccountInfo.Trash != previous.Value.Trash ||
            //    info.AccountInfo.Free != previous.Value.Free)
            //{
            //    // Если между проверками что-то изменились, делаем полный сброс кеша
            //    Clear();
            //    return;
            //}
        }

        bool isEmpty;
        List<string> ownOperation = [];
        _operationLocker.LockedAction(() =>
        {
            isEmpty = _registeredOperationPath.Count == 0;
            foreach (var item in _registeredOperationPath)
            {
                ownOperation.Add(item.Key);
            }
        });

        List<string> paths = [];
        foreach (var op in info.ActiveOperations)
        {
            if (!string.IsNullOrEmpty(op.SourcePath))
            {
                if (!paths.Contains(WebDavPath.Parent(op.SourcePath)))
                {
                    paths.Add(WebDavPath.Parent(op.SourcePath));
                    Logger.Debug($"Operation '{op.Type}' is progressing, clean up under {op.SourcePath}");
                }
            }
            if (!string.IsNullOrEmpty(op.TargetPath))
            {
                if (!paths.Contains(WebDavPath.Parent(op.TargetPath)))
                {
                    paths.Add(WebDavPath.Parent(op.TargetPath));
                    Logger.Debug($"Operation '{op.Type}' is progressing, clean up under {op.TargetPath}");
                }
            }
        }
        if (paths.Count == 0)
            return;

        _rootLocker.LockedActionAsync(() =>
        {
            foreach (var cacheItem in _root)
            {
                foreach (var operationPath in paths)
                {
                    /*
                     * Проверяется условие, что entry из кеша
                     * находится в поддереве пути операции, полученной от сервера,
                     * при этом исключаются каждое поддерево зарегистрированной собственной операций сервиса.
                     */
                    if (WebDavPath.IsParent(operationPath, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false) &&
                        !ownOperation.Any(x => WebDavPath.IsParent(x, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false)))
                    {
                        _root.TryRemove(cacheItem.Key, out _);

                        /*
                         * После удаления из кеша элемента, затронутого операцией на сервере,
                         * которая не является собственной операцией сервиса,
                         * У папки элемента (родительский путь элемента) надо поставить признак,
                         * что загружены не все элементы.
                         */
                        if (_root.TryGetValue(WebDavPath.Parent(cacheItem.Key), out var parentEntry) &&
                            parentEntry.Entry is Folder fld &&
                            parentEntry.AllDescendantsInCache)
                            parentEntry.AllDescendantsInCache = false;
                    }
                }
            }
        });
    }

    public (IEntry, GetState) Get(string fullPath)
    {
        if (!IsCacheEnabled)
            return (default, GetState.Unknown);

        IEntry result = default;

        _rootLocker.Wait();
        try
        {
            if (!_root.TryGetValue(fullPath, out var cachedEntry))
            {
                if (_root.TryGetValue(WebDavPath.Parent(fullPath), out var parentEntry) &&
                    parentEntry.Entry is Folder parentFolder &&
                    parentEntry.AllDescendantsInCache)
                {
                    // Когда в кеше нет элемента, но в родительской директории,
                    // где он должен быть, загружены все элементы в кеш,
                    // то можно точно сказать, что такого элемента нет
                    // не только в кеше, но и на сервере.
                    Logger.Debug($"Cache says: {fullPath} doesn't exist");
                    //System.Diagnostics.Debug.WriteLine($"Cache says: {fullPath} doesn't exist");
                    return (default, GetState.NotExists);
                }

                Logger.Debug($"Cache missed: {fullPath}");
                return (default, GetState.Unknown);
            }

            if (cachedEntry.Entry is null)
            {
                Logger.Debug($"Cache says: {fullPath} doesn't exist");
                //System.Diagnostics.Debug.WriteLine($"Cache says: {fullPath} doesn't exist");
                return (default, GetState.NotExists);
            }

            DateTime threshold = DateTime.Now - _expirePeriod;
            if (cachedEntry.CreationTime <= threshold)
            {
                Logger.Debug($"Cache expired: {fullPath}");
                return (default, GetState.Unknown);
            }

            if (cachedEntry.Entry is File file)
            {
                result = file.New(file.FullPath);
            }

            if (cachedEntry.Entry is Folder folder)
            {
                if (!cachedEntry.AllDescendantsInCache)
                {
                    Logger.Debug($"Cache says: {fullPath} folder's content isn't cached");
                    //System.Diagnostics.Debug.WriteLine($"Cache says: {fullPath} folder's content isn't cached");
                    return (default, GetState.EntryWithUnknownContent);
                }

                var children = new List<IEntry>();

                foreach (var cacheItem in _root)
                {
                    if (WebDavPath.IsParent(fullPath, cacheItem.Key, selfTrue: false, oneLevelDistanceOnly: true))
                    {
                        // Если при формировании списка содержимого папки из кеша
                        // выясняется, что часть содержимого в кеше устарело,
                        // то список из кеша сформировать не можем.
                        if (cacheItem.Value.CreationTime <= threshold)
                            return (default, GetState.Unknown);

                        if (cacheItem.Value.Entry is not null)
                        {
                            // В кеше может быть информация о том, что файла/папки нет,
                            // то есть null, такие пропускаем.
                            children.Add(cacheItem.Value.Entry);
                        }
                    }
                }
                result = folder.New(folder.FullPath, children);
            }
        }
        finally
        {
            _rootLocker.Release();
        }

        Logger.Debug($"Cache hit: {fullPath}");
        return (result, GetState.Entry);
    }

    public void Add(IEntry entry)
    {
        if (!IsCacheEnabled)
            return;

        AddInternal(entry);
    }

    private void AddInternal(IEntry entry)
    {
        // Параметр со временем нужен для того, чтобы все
        // добавляемые в кеш элементы ровно в одно и то же время
        // становились устаревшими.
        if (entry is Link link)
            Add(link, DateTime.Now);
        else
        if (entry is File file)
            AddInternal(file, DateTime.Now);
        else
        if (entry is Folder folder)
        {
            if (folder.IsChildrenLoaded)
            {
                _rootLocker.LockedAction(() => AddWithChildren(folder, DateTime.Now));
            }
            else
            {
                AddOne(folder, DateTime.Now);
            }
        }
    }

    private void AddInternal(File file, DateTime creationTime)
    {
        if (file.Attributes.HasFlag(System.IO.FileAttributes.Offline))
        {
            // Файл затронут активной операцией на сервере
            RemoveOne(file.FullPath);
        }
        else
        {
            string fullPath = file.FullPath;
            var cachedItem = new CacheItem()
            {
                Entry = file.New(fullPath),
                AllDescendantsInCache = true,
                CreationTime = creationTime
            };
            _root.AddOrUpdate(fullPath, cachedItem, (_, _) => cachedItem);
        }
    }

    private void Add(Link link, DateTime creationTime)
    {
        string fullPath = link.FullPath;
        var cachedItem = new CacheItem()
        {
            Entry = link,
            AllDescendantsInCache = true,
            CreationTime = creationTime
        };
        _root.AddOrUpdate(fullPath, cachedItem, (_, _) => cachedItem);
    }

    private CacheItem AddOne(Folder folder, DateTime creationTime)
    {
        string fullPath = folder.FullPath;

        if (folder.Attributes.HasFlag(System.IO.FileAttributes.Offline))
        {
            // Папка затронута активной операцией на сервере
            RemoveTree(folder.FullPath);
            return null;
        }
        else
        {
            var cachedItem = new CacheItem()
            {
                // При добавлении в кеш из folder всего содержимого,
                // делается очистка его списка содержимого, чтобы не было
                // соблазна использовать информацию где-то дальше,
                // т.к. она однозначно перестанет быть актуальной,
                // т.к. алгоритмы кеша не работают с этим списком,
                // этот список только носитель данных при выгрузке с сервера.
                Entry = folder.New(fullPath),
                AllDescendantsInCache = false,
                CreationTime = creationTime
            };
            _root.AddOrUpdate(fullPath, cachedItem,
                (key, value) =>
                {
                    /*
                     * Если папка в кеше имела признак, что все потомки загружены в кеш,
                     * а потом пришло обновление entry этой папки, но признака наличия
                     * в кеше всех потомков нету, то мы выставляем его принудительно,
                     * т.к. знаем, что в кеше все есть, даже если они устарели,
                     * т.к. это будет обработано при следующем чтении списка папки.
                     */
                    if (value.AllDescendantsInCache && !cachedItem.AllDescendantsInCache)
                        cachedItem.AllDescendantsInCache = true;
                    return cachedItem;
                });

            return cachedItem;
        }
    }

    private void AddWithChildren(Folder folder, DateTime creationTime)
    {
        if (folder.Attributes.HasFlag(System.IO.FileAttributes.Offline))
        {
            // Папка затронута активной операцией на сервере
            RemoveTreeNoLock(folder.FullPath);
        }
        else
        {
            foreach (var child in folder.Descendants)
            {
                if (child is File file)
                    AddInternal(file, creationTime);
                else
                if (child is Folder fld)
                    AddOne(fld, creationTime);
            }
            CacheItem cachedItem = AddOne(folder, creationTime);
            cachedItem.AllDescendantsInCache = true;
            /*
             * Внимание! У добавляемого в кеш folder список Descendants всегда пустой!
             * Он специально очищается, чтобы не было соблазна им пользоваться!
             * Содержимое папки берется не из этого списка, а собирается из кеша по path всех entry.
             */
        }
    }

    public void OnCreate(DateTime operationStartTimestamp,
        string createdItemFullPath, Task<IEntry> createdEntryTask, string ignoreItemFullPath)
    {
        if (!IsCacheEnabled)
            return;

        IEntry createdEntry = createdEntryTask?.Result;

        if (createdEntry is null)
        {
            // Если операция должна была создать элемент, но он не получен с сервера,
            // это не нормальная ситуация.
            // Сбрасываем весь кеш, чтобы все перечитать заново на всякий случай.
            Clear();
            return;
        }
        else
        {
            _rootLocker.LockedActionAsync(() =>
            {
                //string itemToCheckUnder = createdItemFullPath;

                bool removed = _root.TryRemove(createdItemFullPath, out var cachedItem);

                // Добавить новый
                if (createdEntry is File file)
                    AddInternal(file, DateTime.Now);
                else
                if (createdEntry is Folder folder)
                {
                    /* Данный метод вызывается для созданных и переименованных файлов и папок.
                     * С сервера читали entry самой папки и одного вложенного элемента.
                     * Если Descendants.Count равен 0, то можно ставить
                     * IsChildrenLoaded = true, т.к. ничего вложенного нет.
                     * Но если Descendants.Count>0, тогда IsChildrenLoaded = false,
                     * т.к. что-то внутри есть, но мы не читали полный список содержимого.
                     * IsChildrenLoaded ставится в true,
                     * чтобы последующие чтения entry созданного элемента
                     * читались из кеша, а не находили только entry от директории
                     * без потомков, что автоматически приводит к игнорированию кеша.
                     */
                    folder.IsChildrenLoaded = folder.Descendants.Count == 0;
                    AddWithChildren(folder, DateTime.Now);
                }
                // После добавления или обновления элемента надо обновить родителя,
                // если у него AllDescendantsInCache=true, иначе нет смысла
                string parent = WebDavPath.Parent(createdItemFullPath);
                if (createdItemFullPath != WebDavPath.Root &&
                    _root.TryGetValue(parent, out var parentEntry) &&
                    parentEntry.Entry is Folder fld &&
                    parentEntry.AllDescendantsInCache)
                {
                    //itemToCheckUnder = fld.FullPath;
                    /*
                     * Внимание! У добавляемого в кеш folder список Descendants всегда пустой!
                     * Он специально очищается, чтобы не было соблазна им пользоваться!
                     * Содержимое папки берется не из этого списка, а собирается из кеша по path всех entry.
                     * В кеше у папок Descendants всегда = ImmutableList<IEntry>.Empty
                     */
                    if (removed && cachedItem?.Entry is not null)
                    {
                        if (cachedItem.Entry.IsFile)
                        {
                            if (fld.ServerFilesCount.HasValue && fld.ServerFilesCount > 0)
                                fld.ServerFilesCount--;
                        }
                        else
                        {
                            if (fld.ServerFoldersCount.HasValue && fld.ServerFoldersCount > 0)
                                fld.ServerFoldersCount--;
                        }
                    }
                    if (createdEntry.IsFile)
                    {
                        if (fld.ServerFilesCount.HasValue)
                            fld.ServerFilesCount++;
                    }
                    else
                    {
                        if (fld.ServerFoldersCount.HasValue)
                            fld.ServerFoldersCount++;
                    }
                }

                //if (operationStartTimestamp != DateTime.MaxValue)
                //{
                //    foreach (var cacheItem in _root)
                //    {
                //        if (!WebDavPath.IsParent(createdItemFullPath, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false) &&
                //            (string.IsNullOrEmpty(ignoreItemFullPath)
                //            || !WebDavPath.IsParent(ignoreItemFullPath, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false)) &&
                //            WebDavPath.IsParent(itemToCheckUnder, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false))
                //        {
                //            if (cacheItem.Value.CreationTime > operationStartTimestamp)
                //            {
                //                /*
                //                 * Если после начала операции,
                //                 * в дереве элементов ниже родителя созданного элемента
                //                 * появился новый элемент, это означает,
                //                 * что были какие-то параллельные операции.
                //                 * В таком случае на всякий случай лучше сделать полный сброс кеша.
                //                 */
                //                ClearNoLock();
                //                return;
                //            }
                //        }
                //    }
                //}
            });
        }
    }

    public void OnRemoveTree(DateTime operationStartTimestamp, string removedItemFullPath, Task<IEntry> removedEntryTask)
    {
        if (!IsCacheEnabled)
            return;

        IEntry removedEntry = removedEntryTask?.Result;

        if (removedEntry is not null)
        {
            // Если операция удалила элемент, но он снова получен с сервера,
            // это не нормальная ситуация.
            // Сбрасываем весь кеш, чтобы все перечитать заново на всякий случай.
            Logger.Error("Cache algorithm failed. Cache is purged.");
            Clear();
            return;
        }
        else
        {
            // Нового элемента на сервере нет,
            // очищаем кеш от элемента и всего, что под ним
            _rootLocker.LockedActionAsync(() =>
            {
                //string itemToCheckUnder = removedItemFullPath;

                // Если элемент был, а нового нет, надо удалить его у родителя,
                // чтобы не перечитывать родителя целиком с сервера,
                // но только, если у родителя AllDescendantsInCache=true, иначе нет смысла
                if (removedItemFullPath != WebDavPath.Root &&
                    _root.TryGetValue(WebDavPath.Parent(removedItemFullPath), out var parentEntry) &&
                    parentEntry.Entry is Folder fld &&
                    parentEntry.AllDescendantsInCache)
                {
                    //itemToCheckUnder = fld.FullPath;
                    /*
                     * Внимание! У добавляемого в кеш folder список Descendants всегда пустой!
                     * Он специально очищается, чтобы не было соблазна им пользоваться!
                     * Содержимое папки берется не из этого списка, а собирается из кеша по path всех entry.
                     * В кеше у папок Descendants всегда = ImmutableList<IEntry>.Empty
                     */
                    if (_root.TryRemove(removedItemFullPath, out var cachedItem))
                    {
                        if (cachedItem.Entry.IsFile)
                        {
                            if (fld.ServerFilesCount.HasValue && fld.ServerFilesCount > 0)
                                fld.ServerFilesCount--;
                        }
                        else
                        {
                            if (fld.ServerFoldersCount.HasValue && fld.ServerFoldersCount > 0)
                                fld.ServerFoldersCount--;
                        }
                    }
                }

                foreach (var cacheItem in _root)
                {
                    //if (operationStartTimestamp != DateTime.MaxValue &&
                    //    WebDavPath.IsParent(itemToCheckUnder, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false))
                    //{
                    //    if (cacheItem.Value.CreationTime > operationStartTimestamp)
                    //    {
                    //        /*
                    //         * Если после начала операции,
                    //         * в дереве элементов ниже родителя удаленного элемента
                    //         * появился новый элемент, это означает,
                    //         * что были какие-то параллельные операции.
                    //         * В таком случае на всякий случай лучше сделать полный сброс кеша.
                    //         */
                    //        ClearNoLock();
                    //        return;
                    //    }
                    //}

                    if (WebDavPath.IsParent(removedItemFullPath, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false))
                    {
                        cacheItem.Value.CreationTime = DateTime.MinValue;
                    }
                }

                // Добавляем информацию о том, что на сервере нет элемента с таким путем,
                // т.к. он был удален, а нового (из newEntryTask) нет.
                var deletedItem = new CacheItem()
                {
                    Entry = null,
                    AllDescendantsInCache = true,
                    CreationTime = DateTime.Now
                };
                _root.TryAdd(removedItemFullPath, deletedItem);
            });
        }
    }

    public void RemoveOne(string fullPath)
    {
        if (!IsCacheEnabled)
            return;

        _rootLocker.LockedAction(() =>
        {
            _root.TryRemove(fullPath, out _);

            if (_root.TryGetValue(WebDavPath.Parent(fullPath), out var parentEntry) &&
                parentEntry.Entry is Folder)
            {
                parentEntry.AllDescendantsInCache = false;
            }
        });
    }

    public void RemoveTree(string fullPath)
    {
        if (!IsCacheEnabled)
            return;

        _rootLocker.LockedAction(() => RemoveTreeNoLock(fullPath));
    }

    private void RemoveTreeNoLock(string fullPath)
    {
        _root.TryRemove(fullPath, out _);

        if (_root.TryGetValue(WebDavPath.Parent(fullPath), out var parentEntry) &&
            parentEntry.Entry is Folder)
        {
            parentEntry.AllDescendantsInCache = false;
        }
        foreach (var cacheItem in _root)
        {
            if (WebDavPath.IsParent(fullPath, cacheItem.Key, selfTrue: true, oneLevelDistanceOnly: false))
            {
                _root.TryRemove(cacheItem.Key, out _);
            }
        }
    }

    public void Clear()
    {
        if (!IsCacheEnabled)
            return;

        _rootLocker.LockedAction(() => ClearNoLock());
    }

    public void ClearNoLock()
    {
        _root.Clear();
        Logger.Debug($"Cache is cleared");
    }
}
