using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YaR.Clouds.Base;
using YaR.Clouds.Base.Repos;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Common;
using YaR.Clouds.Extensions;
using YaR.Clouds.Links;
using YaR.Clouds.Streams;
using File = YaR.Clouds.Base.File;

namespace YaR.Clouds;

/// <summary>
/// Cloud client.
/// </summary>
public partial class Cloud : IDisposable
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Cloud));

    public delegate string AuthCodeRequiredDelegate(string login, bool isAutoRelogin);

    public LinkManager LinkManager { get; }

    /// <summary>
    /// Async tasks cancelation token.
    /// </summary>
    public readonly CancellationTokenSource CancelToken = new();

    public CloudSettings Settings { get; }

    ///// <summary>
    ///// Caching files for multiple small reads
    ///// </summary>
    //private readonly ItemCache<string, IEntry> _itemCache;

    /// <summary>
    /// Кеш облачной файловой системы.
    /// </summary>
    private readonly EntryCache _entryCache;

    internal IRequestRepo RequestRepo { get; }
    public Credentials Credentials { get; }
    public AccountInfoResult AccountInfo { get; private set; } = null;


    /// <summary>
    /// Initializes a new instance of the <see cref="Cloud" /> class.
    /// </summary>
    public Cloud(CloudSettings settings, Credentials credentials)
    {
        Settings = settings;
        WebRequest.DefaultWebProxy.Credentials = CredentialCache.DefaultCredentials;
        Credentials = credentials;
        RequestRepo = new RepoFabric(settings, credentials).Create();

        if (!Credentials.IsAnonymous)
        {
            try
            {
                AccountInfo = RequestRepo.AccountInfo().Result
                    ?? throw new AuthenticationException("The cloud server rejected the credentials provided");
            }
            catch (Exception e) when (!Credentials.AuthenticationUsingBrowser &&
                                      !Credentials.AuthenticationUsingBrowserDisabled &&
                                      e.Contains<AuthenticationException>() &&
                                      e.Contains<InvalidCredentialException>())
            {
                Logger.Warn("Cloud server requested for additional authentication for login & password, " +
                    "browser authentication is not disabled, so going to ask the user to log in using BrowserAuthenticator.");

                try
                {
                    if (!Credentials.Refresh(forceBrowserAuthentication: true))
                    {
                        Logger.Warn("Credentials refreshing is failed");
                        throw;
                    }
                }
                catch (Exception e2) when (e2.Contains<AuthenticationException>())
                {
                    Exception ae = e2.FirstOfType<AuthenticationException>();
                    Logger.Error("Failed to refresh credentials");
                    throw new AuthenticationException(
                        "The cloud server rejected the credentials provided. Then failed to refresh credentials.", ae);
                }

                // Проверка результата
                try
                {
                    AccountInfo = RequestRepo.AccountInfo().Result
                        ?? throw new AuthenticationException("The cloud server rejected the credentials provided");
                }
                catch (Exception e2) when (e2.Contains<AuthenticationException>())
                {
                    Exception ae = e2.FirstOfType<AuthenticationException>();
                    Logger.Error("The server rejected the credentials provided");
                    throw new AuthenticationException(
                        "The cloud server rejected the credentials provided. " +
                        "Credentials have been updated. " +
                        "Then the server rejected the credentials again. ", ae);
                }
            }
            catch (Exception e) when (Credentials.AuthenticationUsingBrowser &&
                                      e.Contains<AuthenticationException>())
            {
                Logger.Warn("Refreshing credentials...");

                try
                {
                    if (!Credentials.Refresh())
                    {
                        Logger.Warn("Credentials refreshing is failed");
                        throw;
                    }
                }
                catch (Exception e2) when (e2.Contains<AuthenticationException>())
                {
                    Exception ae = e2.FirstOfType<AuthenticationException>();
                    Logger.Error("Failed to refresh credentials");
                    throw new AuthenticationException(
                        "The cloud server rejected the credentials provided. Then failed to refresh credentials.", ae);
                }

                // Проверка результата
                try
                {
                    AccountInfo = RequestRepo.AccountInfo().Result
                        ?? throw new AuthenticationException("The cloud server rejected the credentials provided");
                }
                catch (Exception e2) when (e2.Contains<AuthenticationException>())
                {
                    Exception ae = e2.FirstOfType<AuthenticationException>();
                    Logger.Error("The server rejected the credentials provided");
                    throw new AuthenticationException(
                        "The cloud server rejected the credentials provided. " +
                        "Credentials have been updated. " +
                        "Then the server rejected the credentials again. ", ae);
                }
            }
        }

        _entryCache = new EntryCache(TimeSpan.FromSeconds(settings.CacheListingSec), RequestRepo.DetectOutsideChanges);

        ////TODO: wow very dummy linking, refact cache realization globally!
        //_itemCache = new ItemCache<string, IEntry>(TimeSpan.FromSeconds(settings.CacheListingSec));
        ////{
        ////    Полагаемся на стандартно заданное время очистки
        ////    CleanUpPeriod = TimeSpan.FromMinutes(5)
        ////};
        LinkManager = settings.DisableLinkManager ? null : new LinkManager(this);
    }

    public enum ItemType
    {
        File,
        Folder,
        Unknown
    }

    public virtual async Task<IEntry> GetPublicItemAsync(Uri url, ItemType itemType = ItemType.Unknown)
    {
        var entry = await RequestRepo.FolderInfo(RemotePath.Get(new Link(url)));

        return entry;
    }


    private readonly ConcurrentDictionary<string /* full path */, Task<IEntry>> _getItemDict =
        new(StringComparer.InvariantCultureIgnoreCase);

    private readonly SemaphoreSlim _getItemDictLocker = new SemaphoreSlim(1);

    /// <summary>
    /// Get list of files and folders from account.
    /// </summary>
    /// <param name="path">Path in the cloud to return the list of the items.</param>
    /// <param name="itemType">Unknown, File/Folder if you know for sure</param>
    /// <param name="resolveLinks">True if you know for sure that's not a linked item</param>
    /// <param name="fastGetFromCloud">True to skip link manager, cache and so on, just get
    /// the entry info from cloud as fast as possible.</param>
    /// <returns>List of the items.</returns>
    public virtual Task<IEntry> GetItemAsync(string path, ItemType itemType = ItemType.Unknown,
        bool resolveLinks = true, bool fastGetFromCloud = false)
    {
        /*
         * Параметр ItemType сохранен для совместимости со старыми вызовами,
         * чтобы не потерять и сохранить информацию когда что надо получить,
         * на случай, если понадобится, но сейчас по факту параметр не используется.
         */

        /*
         * Клиенты очень часто читают одну и ту же директорию через короткий промежуток времени.
         * Часто получается так, что первый запрос чтения папки с сервера еще не завершен,
         * а тут уже второй пришел, а поскольку кеш еще не образован результатом первого обращения,
         * то уже оба идут к серверу, не взирая на то, что выбирается одна и та же папка.
         * Поэтому, составляем словарь, где ключ - FullPath, а значение - Task.
         * Первый, кто лезет за содержимым папки, формирует Task, кладет его в словарь,
         * по окончании очищает словарь от записи.
         * Последующие, обнаружив запись в словаре, используют сохраненный Task,
         * точнее его Result. До формирования значения в Result последующие обращения блокируются
         * и ждут завершения, затем получают готовое значение и уходят довольные.
         * Это снижает нагрузку на канал до сервера, нагрузку на сервер и позволяет для
         * последующих обращений получать результат раньше, т.к. результат первого обращения
         * точно будет получен раньше, т.к. раньше начался.
         */

        if (_getItemDict.TryGetValue(path, out var oldTask))
            return oldTask;

        _getItemDictLocker.Wait();
        try
        {
            if (_getItemDict.TryGetValue(path, out oldTask))
                return oldTask;

            _getItemDict[path] = Task.Run(() => GetItemInternalAsync(path, resolveLinks, fastGetFromCloud).Result);
        }
        finally
        {
            _getItemDictLocker.Release();
        }

        try
        {
            return Task.FromResult(_getItemDict[path].Result);
        }
        finally
        {
            _getItemDict.TryRemove(path, out _);
        }
    }

    private const string MailRuPublicRegexMask = @"\A/(?<uri>https://cloud\.mail\.\w+/public/\S+/\S+(/.*)?)\Z";

#if NET7_0_OR_GREATER
    [GeneratedRegex(MailRuPublicRegexMask, RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MailRuPublicRegex();
    private static readonly Regex _mailRegex = MailRuPublicRegex();
#else
    private static readonly Regex _mailRegex =
        new Regex(MailRuPublicRegexMask, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
#endif

    /// <summary>
    /// Данный метод запускается для каждого path только в одном потоке.
    /// Все остальные потоки ожидают результата от единственного выполняемого.
    /// См. комментарий в методе GetItemAsync.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="resolveLinks"></param>
    /// <returns></returns>
    private async Task<IEntry> GetItemInternalAsync(string path, bool resolveLinks, bool fastGetFromCloud = false)
    {
        if (!fastGetFromCloud &&
            (Settings.Protocol == Protocol.WebM1Bin || Settings.Protocol == Protocol.WebV2))
        {
            //TODO: вообще, всё плохо стало, всё запуталось, всё надо переписать
            var uriMatch = _mailRegex.Match(path);
            if (uriMatch.Success)
                return await GetPublicItemAsync(new Uri(uriMatch.Groups["uri"].Value, UriKind.Absolute));
        }

        if (Credentials.IsAnonymous)
            return null;

        path = WebDavPath.Clean(path);
        RemotePath remotePath;

        if (fastGetFromCloud)
        {
            remotePath = RemotePath.Get(path);
            return await RequestRepo.FolderInfo(remotePath, depth: 1, limit: 2);
        }

        (var cached, var getState) = _entryCache.Get(path);
        if (getState == EntryCache.GetState.Entry)
            return cached;
        if (getState == EntryCache.GetState.NotExists)
            return null;

        //TODO: subject to refact!!!
        Link ulink = resolveLinks && LinkManager is not null ? await LinkManager.GetItemLink(path) : null;

        /*
         * Если LinkManager на затребованный path выдал ссылку, а ссылка бракованная и не рабочая,
         * то нельзя выдать null, показывающий отсутствие файла/папки, иначе удалить такую
         * бракованную ссылку будет невозможно.
         * Потому формируем специальную заглушку с признаком Bad.
         */
        if (ulink is { IsBad: true })
        {
            var res = ulink.ToBadEntry();
            _entryCache.Add(res);
            return res;
        }

        //if (itemType == ItemType.Unknown && ulink is not null)
        //    itemType = ulink.ItemType;

        // TODO: cache (parent) folder for file
        //if (itemType == ItemType.File)
        //{
        //    var cachefolder = datares.ToFolder(path, ulink);
        //    _itemCache.Add(cachefolder.FullPath, cachefolder);
        //    //_itemCache.Add(cachefolder.Files);
        //}
        remotePath = ulink is null ? RemotePath.Get(path) : RemotePath.Get(ulink);
        DateTime timestamp = DateTime.Now;
        var cloudResult = await RequestRepo.FolderInfo(remotePath, depth: Settings.ListDepth);
        if (cloudResult is null)
        {
            // Если с сервера получено состояние, что папки нет,
            // а кеш ранее говорил, что папка в кеше есть, но без наполнения,
            // то папку удалили и надо безотлагательно очистить кеш.
            if (getState == EntryCache.GetState.EntryWithUnknownContent)
            {
                Logger.Debug("Папка была удалена, делается чистка кеша");
            }
            _entryCache.OnRemoveTree(timestamp, remotePath.Path, null);

            return null;
        }


        //if (itemType == ItemType.Unknown)
        //    itemType = cloudResult is Folder
        //        ? ItemType.Folder
        //        : ItemType.File;

        //if (itemType == ItemType.Folder && cloudResult is Folder folder) // fill folder with links if any
        //    FillWithULinks(folder);

        if (LinkManager is not null && cloudResult is Folder f)
        {
            FillWithULinks(f);
        }

        //if (Settings.CacheListingSec > 0)
        //    CacheAddEntry(cloudResult);

        _entryCache.Add(cloudResult);


        /*
         * Если запрошен файл или папка, которого нет в кеше,
         * при этом не известно, есть ли он на сервере,
         * есть вероятность, что скоро будет запрошен соседний файл или папка
         * и той же родительской папки, поэтому имеет смысл сделать упреждающее
         * чтение содержимого родительской папки с сервера или убедиться,
         * что она есть в кеше.
         */
        string parentPath = WebDavPath.Parent(path);
        if (parentPath != path && _entryCache.IsCacheEnabled)
        {
            // Здесь не ожидается результата работы, задача отработает в фоне и заполнит кеш.
            // Обращение к серверу делается после чтения с сервера результата текущего обращения,
            // то есть сначала затребованный результат, а потом фоновый, не наоборот, чтобы не ждать.
            _ = GetItemAsync(parentPath).ConfigureAwait(false);
        }

        return cloudResult;
    }

    private void FillWithULinks(Folder folder)
    {
        if (folder == null || !folder.IsChildrenLoaded)
            return;

        string fullPath = folder.FullPath;

        var flinks = LinkManager.GetItems(fullPath);
        if (flinks is not null)
        {
            var newChildren = new List<IEntry>();
            foreach (var flink in flinks)
            {
                string linkpath = WebDavPath.Combine(fullPath, flink.Name);

                if (flink.IsFile)
                {
                    if (folder.Descendants.Any(entry => entry.FullPath.Equals(linkpath, StringComparison.InvariantCultureIgnoreCase)))
                        continue;

                    var newFile = new File(linkpath, flink.Size, new PublicLinkInfo(flink.Href));
                    if (flink.CreationDate is not null)
                        newFile.LastWriteTimeUtc = flink.CreationDate.Value;
                    newChildren.Add(newFile);
                }
                else
                {
                    Folder newFolder = new Folder(0, linkpath) { CreationTimeUtc = flink.CreationDate ?? DateTime.MinValue };
                    newChildren.Add(newFolder);
                }
            }
            if (newChildren.Count > 0)
            {
                folder.Descendants = folder.Descendants.AddRange(newChildren);
            }
        }

        foreach (var child in folder.Descendants)
        {
            if (child is Folder f)
                FillWithULinks(f);
        }
    }

    //private void FillWithULinks(Folder folder)
    //{
    //    if (!folder.IsChildrenLoaded) return;

    //    if (LinkManager is not null)
    //    {
    //        var flinks = LinkManager.GetItems(folder.FullPath);
    //        if (flinks is not null && flinks.Any())
    //        {
    //            foreach (var flink in flinks)
    //            {
    //                string linkpath = WebDavPath.Combine(folder.FullPath, flink.Name);

    //                if (!flink.IsFile)
    //                {
    //                    Folder item = new Folder(0, linkpath) { CreationTimeUtc = flink.CreationDate ?? DateTime.MinValue };
    //                    folder.Folders.AddOrUpdate(item.FullPath, item, (_, _) => item);
    //                }
    //                else
    //                {
    //                    if (folder.Files.ContainsKey(linkpath))
    //                        continue;

    //                    var newFile = new File(linkpath, flink.Size, new PublicLinkInfo(flink.Href));
    //                    if (flink.CreationDate is not null)
    //                        newFile.LastWriteTimeUtc = flink.CreationDate.Value;
    //                    folder.Files.AddOrUpdate(newFile.FullPath, newFile, (_, _) => newFile);
    //                }
    //            }
    //        }
    //    }

    //    foreach (var childFolder in folder.Folders.Values)
    //        FillWithULinks(childFolder);
    //}


    //private void CacheAddEntry(IEntry entry)
    //{
    //    switch (entry)
    //    {
    //        case File cfile:
    //            _itemCache.Add(cfile.FullPath, cfile);
    //            break;
    //        case Folder { IsChildrenLoaded: true } cfolder:
    //        {
    //            _itemCache.Add(cfolder.FullPath, cfolder);
    //            _itemCache.Add(cfolder.Files.Select(f => new KeyValuePair<string, IEntry>(f.Value.FullPath, f.Value)));

    //            foreach (var childFolder in cfolder.Entries)
    //                CacheAddEntry(childFolder);
    //            break;
    //        }
    //    }
    //}

    //private IEntry CacheGetEntry(string path) => _itemCache.Get(path);

    //public virtual IEntry GetItem(string path, ItemType itemType = ItemType.Unknown, bool resolveLinks = true)
    //    => GetItemAsync(path, itemType, resolveLinks).Result;

    /// <summary>
    /// Поиск файла/папки по названию (без пути) в перечисленных папках.
    /// Возвращает полный путь к файлу или папке.
    /// </summary>
    /// <param name="nameWithoutPathToFind">Название файла/папки без пути.</param>
    /// <param name="folderPaths">Список полных папок с полными путями.</param>
    /// <returns></returns>
    public string Find(string nameWithoutPathToFind, params string[] folderPaths)
    {
        if (folderPaths is null || folderPaths.Length == 0 || string.IsNullOrEmpty(nameWithoutPathToFind))
            return null;

        List<string> paths = [];
        // Сначала смотрим в кеше, без обращений к серверу
        foreach (var folderPath in folderPaths)
        {
            var path = WebDavPath.Combine(folderPath, nameWithoutPathToFind);
            (var cached, var getState) = _entryCache.Get(path);
            // Если файл или папка найдены в кеше
            if (getState == EntryCache.GetState.Entry)
                return cached.FullPath;
            // Если файл или папка точно отсутствуют в кеше и на сервере
            if (getState == EntryCache.GetState.NotExists)
                continue;
            // В остальных случаях будем искать дальше
            paths.Add(folderPath);
        }
        if (paths.Count == 0)
            return null;

        // В кеше файла не оказалось, читаем все директории и смотрим а них

        var tasks = paths
            .AsParallel()
            .WithDegreeOfParallelism(paths.Count)
            .Select(async path => await GetItemAsync(path, ItemType.Folder, false));

        if (tasks is null)
            return null;

        foreach (var task in tasks)
        {
            if (task.Result is null)
                continue;
            IEntry entry = task.Result;
            if (entry.Name.Equals(nameWithoutPathToFind, StringComparison.InvariantCultureIgnoreCase))
                return entry.FullPath;

            foreach (var child in entry.Descendants)
            {
                if (child.Name.Equals(nameWithoutPathToFind, StringComparison.InvariantCultureIgnoreCase))
                    return child.FullPath;
            }
        }

        return null;
    }

    #region == Publish ==========================================================================================================================

    private async Task<bool> Unpublish(Uri publicLink, string fullPath)
    {
        //var res = (await new UnpublishRequest(CloudApi, publicLink).MakeRequestAsync(_connectionLimiter))
        var res = (await RequestRepo.Unpublish(publicLink, fullPath))
            .ThrowIf(r => !r.IsSuccess, _ => new Exception($"Unpublish error, link = {publicLink}"));

        return res.IsSuccess;
    }

    public async Task Unpublish(File file)
    {
        foreach (var innerFile in file.Files)
        {
            await Unpublish(innerFile.GetPublicLinks(this).FirstOrDefault().Uri, innerFile.FullPath);
            innerFile.PublicLinks.Clear();
        }
    }


    private async Task<Uri> Publish(string fullPath)
    {
        var res = (await RequestRepo.Publish(fullPath))
        .ThrowIf(r => !r.IsSuccess, _ => new Exception($"Publish error, path = {fullPath}"));

        var uri = new Uri(res.Url, UriKind.RelativeOrAbsolute);
        if (!uri.IsAbsoluteUri)
            uri = new Uri($"{RequestRepo.PublicBaseUrlDefault.TrimEnd('/')}/{res.Url.TrimStart('/')}", UriKind.Absolute);

        return uri;
    }

    public async Task<PublishInfo> Publish(File file, bool makeShareFile = true,
        bool generateDirectVideoLink = false, bool makeM3UFile = false, SharedVideoResolution videoResolution = SharedVideoResolution.All)
    {
        if (file.Files.Count > 1 && (generateDirectVideoLink || makeM3UFile))
            throw new ArgumentException($"Cannot generate direct video link for splitted file {file.FullPath}");

        foreach (var innerFile in file.Files)
        {
            var url = await Publish(innerFile.FullPath);
            innerFile.PublicLinks.Clear();
            innerFile.PublicLinks.TryAdd(url.AbsolutePath, new PublicLinkInfo(url));
        }
        var info = file.ToPublishInfo(this, generateDirectVideoLink, videoResolution);

        if (makeShareFile)
        {
            string path = string.Concat(file.FullPath, PublishInfo.SharedFilePostfix);
            UploadFileJson(path, info)
                .ThrowIf(r => !r, _ => new Exception($"Cannot upload JSON file, path = {path}"));
        }


        if (makeM3UFile)
        {
            string path = string.Concat(file.FullPath, PublishInfo.PlayListFilePostfix);
            var content = new StringBuilder();
            {
                content.Append("#EXTM3U\r\n");
                foreach (var item in info.Items)
                {
                    content.Append($"#EXTINF:-1,{WebDavPath.Name(item.Path)}\r\n");
                    content.Append($"{item.PlayListUrl}\r\n");
                }
            }
            UploadFile(path, content.ToString())
                .ThrowIf(r => !r, _ => new Exception($"Cannot upload JSON file, path = {path}"));
        }

        return info;
    }

    public async Task<PublishInfo> Publish(Folder folder, bool makeShareFile = true)
    {
        var url = await Publish(folder.FullPath);
        folder.PublicLinks.Clear();
        folder.PublicLinks.TryAdd(url.AbsolutePath, new PublicLinkInfo(url));
        var info = folder.ToPublishInfo();

        if (!makeShareFile)
            return info;

        string path = WebDavPath.Combine(folder.FullPath, PublishInfo.SharedFilePostfix);
        UploadFileJson(path, info)
            .ThrowIf(r => !r, _ => new Exception($"Cannot upload JSON file, path = {path}"));

        return info;
    }

    public async Task<PublishInfo> Publish(IEntry entry, bool makeShareFile = true,
        bool generateDirectVideoLink = false, bool makeM3UFile = false, SharedVideoResolution videoResolution = SharedVideoResolution.All)
    {
        return entry switch
        {
            null => throw new ArgumentNullException(nameof(entry)),
            File file => await Publish(file, makeShareFile, generateDirectVideoLink, makeM3UFile, videoResolution),
            Folder folder => await Publish(folder, makeShareFile),
            _ => throw new Exception($"Unknown entry type, type = {entry.GetType()},path = {entry.FullPath}")
        };
    }
    #endregion == Publish =======================================================================================================================

    #region == Copy =============================================================================================================================

    /// <summary>
    /// Copy folder.
    /// </summary>
    /// <param name="folder">Source folder.</param>
    /// <param name="destinationPath">Destination path on the server.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> Copy(Folder folder, string destinationPath)
    {
        DateTime timestampBeforeOperation = DateTime.Now;
        destinationPath = WebDavPath.Clean(destinationPath);

        // if it linked - just clone
        if (LinkManager is not null)
        {
            var link = await LinkManager.GetItemLink(folder.FullPath, false);
            if (link is not null)
            {
                var cloneres = await CloneItem(destinationPath, link.Href.OriginalString);
                if (!cloneres.IsSuccess || WebDavPath.Name(cloneres.Path) == link.Name)
                    return cloneres.IsSuccess;
                var renRes = await Rename(cloneres.Path, link.Name);
                return renRes;
            }
        }

        try
        {
            _entryCache.RegisterOperation(folder.FullPath, CounterOperation.Copy);
            _entryCache.RegisterOperation(destinationPath, CounterOperation.None);

            //var copyRes = await new CopyRequest(CloudApi, folder.FullPath, destinationPath).MakeRequestAsync(_connectionLimiter);
            var copyRes = await RequestRepo.Copy(folder.FullPath, destinationPath);
            if (!copyRes.IsSuccess)
                return false;

            // OnRemove делать до OnCreate
            _entryCache.OnRemoveTree(timestampBeforeOperation,
                folder.FullPath, GetItemAsync(folder.FullPath, fastGetFromCloud: true));
            _entryCache.OnCreate(timestampBeforeOperation,
                destinationPath, GetItemAsync(destinationPath, fastGetFromCloud: true), folder.FullPath);
        }
        finally
        {
            _entryCache.UnregisterOperation(folder.FullPath);
            _entryCache.UnregisterOperation(destinationPath);
        }

        //clone all inner links
        if (LinkManager is not null)
        {
            var links = LinkManager.GetChildren(folder.FullPath);
            if (links is not null)
            {
                foreach (var linka in links)
                {
                    var linkdest = WebDavPath.ModifyParent(linka.MapPath, WebDavPath.Parent(folder.FullPath), destinationPath);
                    var cloneres = await CloneItem(linkdest, linka.Href.OriginalString);
                    if (!cloneres.IsSuccess || WebDavPath.Name(cloneres.Path) == linka.Name)
                        continue;

                    if (await Rename(cloneres.Path, linka.Name))
                        continue;

                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Copy item.
    /// </summary>
    /// <param name="sourcePath">Source item.</param>
    /// <param name="destinationPath">Destination path on the server.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> Copy(string sourcePath, string destinationPath)
    {
        var entry = await GetItemAsync(sourcePath);
        if (entry is null)
            return false;

        return await Copy(entry, destinationPath);
    }

    /// <summary>
    /// Copy item.
    /// </summary>
    /// <param name="source">Source item.</param>
    /// <param name="destinationPath">Destination path on the server.</param>
    /// <param name="newName">Rename target item.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> Copy(IEntry source, string destinationPath, string newName = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrEmpty(destinationPath)) throw new ArgumentNullException(nameof(destinationPath));

        return source switch
        {
            File file => await Copy(file, destinationPath, string.IsNullOrEmpty(newName) ? file.Name : newName),
            Folder folder => await Copy(folder, destinationPath),
            _ => throw new ArgumentException("Source is not a file or folder", nameof(source))
        };
    }

    /// <summary>
    /// Copy file to another path.
    /// </summary>
    /// <param name="file">Source file info.</param>
    /// <param name="destinationPath">Destination path.</param>
    /// <param name="newName">Rename target file.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> Copy(File file, string destinationPath, string newName)
    {
        DateTime timestamp = DateTime.Now;
        string destPath = destinationPath;
        newName = string.IsNullOrEmpty(newName) ? file.Name : newName;
        bool doRename = file.Name != newName;

        if (LinkManager is not null)
        {
            var link = await LinkManager.GetItemLink(file.FullPath, false);
            // копируем не саму ссылку, а её содержимое
            if (link is not null)
            {

                var cloneRes = await CloneItem(destPath, link.Href.OriginalString);
                if (!cloneRes.IsSuccess)
                    return false;

                if (doRename || WebDavPath.Name(cloneRes.Path) != newName)
                {
                    string newFullPath = WebDavPath.Combine(destPath, WebDavPath.Name(cloneRes.Path));
                    var renameRes = await Rename(newFullPath, link.Name);
                    if (!renameRes)
                        return false;
                }

                return true;
            }
        }

        var qry = file
                .Files
                .AsParallel()
                .WithDegreeOfParallelism(file.Files.Count)
                .Select(async pfile =>
                {
                    try
                    {
                        _entryCache.RegisterOperation(pfile.FullPath, CounterOperation.Copy);
                        _entryCache.RegisterOperation(destPath, CounterOperation.None);

                        //var copyRes = await new CopyRequest(CloudApi, pfile.FullPath, destPath, ConflictResolver.Rewrite).MakeRequestAsync(_connectionLimiter);
                        var copyRes = await RequestRepo.Copy(pfile.FullPath, destPath, ConflictResolver.Rewrite);
                        if (!copyRes.IsSuccess)
                            return false;

                        if (!doRename && WebDavPath.Name(copyRes.NewName) == newName)
                            return true;

                        string newFullPath = WebDavPath.Combine(destPath, WebDavPath.Name(copyRes.NewName));
                        return await Rename(newFullPath, pfile.Name.Replace(file.Name, newName));
                    }
                    finally
                    {
                        _entryCache.UnregisterOperation(pfile.FullPath);
                        _entryCache.UnregisterOperation(destPath);
                    }
                });

        bool res = (await Task.WhenAll(qry))
            .All(r => r);

        return res;
    }

    #endregion == Copy ==========================================================================================================================

    #region == Rename ===========================================================================================================================

    /// <summary>
    /// Rename item on the server.
    /// </summary>
    /// <param name="source">Source item info.</param>
    /// <param name="newName">New item name.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> Rename(IEntry source, string newName)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrEmpty(newName))
            throw new ArgumentNullException(nameof(newName));

        return source switch
        {
            File file => await Rename(file, newName),
            Folder folder => await Rename(folder, newName),
            _ => throw new ArgumentException("Source item is not a file nor folder", nameof(source))
        };
    }

    /// <summary>
    /// Rename folder on the server.
    /// </summary>
    /// <param name="folder">Source folder info.</param>
    /// <param name="newFileName">New folder name.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> Rename(Folder folder, string newFileName)
        => await Rename(folder.FullPath, newFileName);

    /// <summary>
    /// Rename file on the server.
    /// </summary>
    /// <param name="file">Source file info.</param>
    /// <param name="newFileName">New file name.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> Rename(File file, string newFileName)
    {
        var result = await Rename(file.FullPath, newFileName).ConfigureAwait(false);

        if (file.Files.Count <= 1)
            return result;

        foreach (var splitFile in file.Parts)
        {
            string newSplitName = newFileName + splitFile.ServiceInfo.ToString(false);
            await Rename(splitFile.FullPath, newSplitName).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Rename item on server.
    /// </summary>
    /// <param name="fullPath">Full path of the file or folder.</param>
    /// <param name="newName">New file or path name.</param>
    /// <returns>True or false result operation.</returns>
    private async Task<bool> Rename(string fullPath, string newName)
    {
        var link = LinkManager is null ? null : await LinkManager.GetItemLink(fullPath, false);

        //rename item
        if (link is null)
        {
            string newNamePath = WebDavPath.Combine(WebDavPath.Parent(fullPath), newName);
            try
            {
                DateTime timestampBeforeOperation = DateTime.Now;
                _entryCache.RegisterOperation(fullPath, CounterOperation.Rename);
                _entryCache.RegisterOperation(newNamePath, CounterOperation.None);

                var data = await RequestRepo.Rename(fullPath, newName);

                if (!data.IsSuccess)
                    return data.IsSuccess;

                LinkManager?.ProcessRename(fullPath, newName);

                // OnRemove делать до OnCreate
                _entryCache.OnRemoveTree(timestampBeforeOperation,
                    fullPath, GetItemAsync(fullPath, fastGetFromCloud: true));
                _entryCache.OnCreate(timestampBeforeOperation,
                    newNamePath, GetItemAsync(newNamePath, fastGetFromCloud: true), fullPath);

                return data.IsSuccess;
            }
            finally
            {
                _entryCache.UnregisterOperation(fullPath);
                _entryCache.UnregisterOperation(newNamePath);
            }
        }

        //rename link
        if (LinkManager is not null)
        {
            bool res = LinkManager.RenameLink(link, newName);
            return res;
        }
        return false;
    }

    #endregion == Rename ========================================================================================================================

    #region == Move =============================================================================================================================

    /// <summary>
    /// Move item.
    /// </summary>
    /// <param name="source">source item info.</param>
    /// <param name="destinationPath">Destination path on the server.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> MoveAsync(IEntry source, string destinationPath)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrEmpty(destinationPath)) throw new ArgumentNullException(nameof(destinationPath));

        return source switch
        {
            File file => await MoveAsync(file, destinationPath),
            Folder folder => await MoveAsync(folder, destinationPath),
            _ => throw new ArgumentException("Source item is not a file nor folder", nameof(source))
        };
    }

    public async Task<bool> MoveAsync(string sourcePath, string destinationPath)
    {
        var entry = await GetItemAsync(sourcePath);
        if (entry is null)
            return false;

        return await MoveAsync(entry, destinationPath);
    }

    public bool Move(string sourcePath, string destinationPath)
    {
        return MoveAsync(sourcePath, destinationPath).Result;
    }


    /// <summary>
    /// Move folder to another place on the server.
    /// </summary>
    /// <param name="folder">Folder to move.</param>
    /// <param name="destinationPath">Destination path on the server.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> MoveAsync(Folder folder, string destinationPath)
    {
        var link = LinkManager is null ? null : await LinkManager.GetItemLink(folder.FullPath, false);
        if (link is not null)
        {
            var remapped = await LinkManager.RemapLink(link, destinationPath);
            return remapped;
        }

        try
        {
            DateTime timestampBeforeOperation = DateTime.Now;
            _entryCache.RegisterOperation(folder.FullPath, CounterOperation.Move);
            _entryCache.RegisterOperation(destinationPath, CounterOperation.None);

            var res = await RequestRepo.Move(folder.FullPath, destinationPath);

            if (!res.IsSuccess)
                return false;

            // OnRemove делать до OnCreate
            _entryCache.OnRemoveTree(timestampBeforeOperation,
                folder.FullPath, GetItemAsync(folder.FullPath, fastGetFromCloud: true));
            _entryCache.OnCreate(timestampBeforeOperation,
                destinationPath, GetItemAsync(destinationPath, fastGetFromCloud: true), folder.FullPath);
        }
        finally
        {
            _entryCache.UnregisterOperation(folder.FullPath);
            _entryCache.UnregisterOperation(destinationPath);
        }

        //clone all inner links
        if (LinkManager is not null)
        {
            var links = LinkManager.GetChildren(folder.FullPath).ToList();
            foreach (var linka in links)
            {
                // некоторые клиенты сначала делают структуру каталогов, а потом по одному переносят файлы
                // в таких условиях на каждый файл получится свой собственный линк,
                // если делать правильно, т.е. в итоге расплодится миллион линков
                // поэтому делаем неправильно - копируем содержимое линков

                var linkdest = WebDavPath.ModifyParent(linka.MapPath, WebDavPath.Parent(folder.FullPath), destinationPath);
                var cloneres = await CloneItem(linkdest, linka.Href.OriginalString);
                if (!cloneres.IsSuccess)
                    continue;

                if (WebDavPath.Name(cloneres.Path) != linka.Name)
                {
                    var renRes = await Rename(cloneres.Path, linka.Name);
                    if (!renRes) return false;
                }
            }
            if (links.Any())
                LinkManager.Save();
        }

        return true;
    }

    /// <summary>
    /// Move file in another space on the server.
    /// </summary>
    /// <param name="file">File info to move.</param>
    /// <param name="destinationPath">Destination path on the server.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> MoveAsync(File file, string destinationPath)
    {
        DateTime timestampBeforeOperation = DateTime.Now;
        var link = LinkManager is null ? null : await LinkManager.GetItemLink(file.FullPath, false);
        if (link is not null)
        {
            var remapped = await LinkManager.RemapLink(link, destinationPath);
            return remapped;
        }

        var qry = file.Files
            .AsParallel()
            .WithDegreeOfParallelism(file.Files.Count)
            .Select(async pfile =>
            {
                try
                {
                    _entryCache.RegisterOperation(pfile.FullPath, CounterOperation.Move);
                    _entryCache.RegisterOperation(destinationPath, CounterOperation.None);

                    var moveRes = await RequestRepo.Move(pfile.FullPath, destinationPath);

                    if (!moveRes.IsSuccess)
                        return moveRes;

                    // OnRemove делать до OnCreate
                    _entryCache.OnRemoveTree(timestampBeforeOperation,
                        file.FullPath, GetItemAsync(file.FullPath, fastGetFromCloud: true));
                    _entryCache.OnCreate(timestampBeforeOperation,
                        destinationPath, GetItemAsync(destinationPath, fastGetFromCloud: true), file.FullPath);

                    return moveRes;
                }
                finally
                {
                    _entryCache.UnregisterOperation(pfile.FullPath);
                    _entryCache.UnregisterOperation(destinationPath);
                }
            });


        bool res = (await Task.WhenAll(qry))
            .All(r => r.IsSuccess);

        return res;
    }

    #endregion == Move ==========================================================================================================================

    #region == Remove ===========================================================================================================================

    /// <summary>
    /// Remove item on server by path
    /// </summary>
    /// <param name="entry">File or folder</param>
    /// <returns>True or false operation result.</returns>
    public virtual async Task<bool> Remove(IEntry entry)
    {
        return entry switch
        {
            File file => await Remove(file),
            Folder folder => await Remove(folder),
            _ => false
        };
    }

    /// <summary>
    /// Remove the folder on server.
    /// </summary>
    /// <param name="folder">Folder info.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> Remove(Folder folder)
    {
        return await Remove(folder.FullPath);
    }

    /// <summary>
    /// Remove the file on server.
    /// </summary>
    /// <param name="file">File info.</param>
    /// <param name="removeShareDescription">Also remove share description file (.share.wdmrc)</param>
    /// <returns>True or false operation result.</returns>
    public virtual async Task<bool> Remove(File file, bool removeShareDescription = true) //, bool doInvalidateCache = true)
    {
        // remove all parts if file splitted
        var qry = file.Files
            .AsParallel()
            .WithDegreeOfParallelism(file.Files.Count)
            .Select(async pfile =>
            {
                var removed = await Remove(pfile.FullPath);
                return removed;
            });
        bool res = (await Task.WhenAll(qry)).All(r => r);

        if (res)
        {
            if (file.Name.EndsWith(PublishInfo.SharedFilePostfix))  //unshare master item
            {
                var mpath = WebDavPath.Clean(file.FullPath.Substring(0, file.FullPath.Length - PublishInfo.SharedFilePostfix.Length));
                var entry = await GetItemAsync(mpath);

                switch (entry)
                {
                case Folder folder:
                    await Unpublish(folder.GetPublicLinks(this).FirstOrDefault().Uri, folder.FullPath);
                    break;
                case File ifile:
                    await Unpublish(ifile);
                    break;
                }
            }
            else
            {
                if (removeShareDescription) //remove share description (.wdmrc.share)
                {
                    string fullName = string.Concat(file.FullPath, PublishInfo.SharedFilePostfix);
                    string path = WebDavPath.Parent(file.FullPath);
                    string name = WebDavPath.Name(file.FullPath);
                    string foundFullPath = Find(name, path);

                    if (foundFullPath is not null &&
                        await GetItemAsync(foundFullPath) is File sharefile)
                    {
                        await Remove(sharefile, false);
                    }
                }
            }

        }

        return res;
    }

    /// <summary>
    /// Remove file or folder.
    /// </summary>
    /// <param name="fullPath">Full file or folder name.</param>
    /// <returns>True or false result operation.</returns>
    public async Task<bool> Remove(string fullPath)
    {
        DateTime timestamp = DateTime.Now;

        if (LinkManager is not null)
        {
            var link = await LinkManager.GetItemLink(fullPath, false);
            if (link is not null)
            {
                // if folder is linked - do not delete inner files/folders
                // if client deleting recursively just try to unlink folder
                return LinkManager.RemoveLink(fullPath);
            }
        }

        try
        {
            _entryCache.RegisterOperation(fullPath, CounterOperation.RemoveToTrash);

            var res = await RequestRepo.Remove(fullPath);

            if (!res.IsSuccess)
                return false;

            _entryCache.OnRemoveTree(timestamp, fullPath, GetItemAsync(fullPath, fastGetFromCloud: true));
        }
        finally
        {
            _entryCache.UnregisterOperation(fullPath);
        }

        // remove inner links
        if (LinkManager is not null)
        {
            var innerLinks = LinkManager.GetChildren(fullPath);
            LinkManager.RemoveLinks(innerLinks);
        }

        return true;
    }

    #endregion == Remove ========================================================================================================================

    public IEnumerable<PublicLinkInfo> GetSharedLinks(string fullPath)
    {
        return RequestRepo.GetShareLinks(fullPath);
    }

    /// <summary>
    /// Get disk usage for account.
    /// </summary>
    /// <returns>Returns Total/Free/Used size.</returns>
    public async Task<DiskUsage> GetDiskUsageAsync()
    {
        var data = await RequestRepo.AccountInfo();
        return data.DiskUsage;
    }
    public DiskUsage GetDiskUsage()
    {
        return GetDiskUsageAsync().Result;
    }


    /// <summary>
    /// Abort all prolonged async operations.
    /// </summary>
    public void AbortAllAsyncThreads()
    {
        CancelToken.Cancel(false);
    }

    /// <summary>
    /// Create folder on the server.
    /// </summary>
    /// <param name="name">New path name.</param>
    /// <param name="basePath">Destination path.</param>
    /// <returns>True or false operation result.</returns>
    public async Task<bool> CreateFolderAsync(string name, string basePath)
    {
        return await CreateFolderAsync(WebDavPath.Combine(basePath, name));
    }

    public bool CreateFolder(string name, string basePath)
    {
        return CreateFolderAsync(name, basePath).Result;
    }

    public async Task<bool> CreateFolderAsync(string fullPath)
    {
        try
        {
            DateTime timestampBeforeOperation = DateTime.Now;

            _entryCache.RegisterOperation(fullPath, CounterOperation.NewFolder);

            var res = await RequestRepo.CreateFolder(fullPath);

            if (!res.IsSuccess)
                return false;

            _entryCache.OnCreate(timestampBeforeOperation,
                fullPath, GetItemAsync(fullPath, fastGetFromCloud: true), null);
        }
        finally
        {
            _entryCache.UnregisterOperation(fullPath);
        }

        return true;
    }

    //public bool CreateFolder(string fullPath)
    //{
    //    return CreateFolderAsync(fullPath).Result;
    //}


    public async Task<CloneItemResult> CloneItem(string toPath, string fromUrl)
    {
        try
        {
            DateTime timestampBeforeOperation = DateTime.Now;
            _entryCache.RegisterOperation(toPath, CounterOperation.Copy);
            _entryCache.RegisterOperation(fromUrl, CounterOperation.None);

            var res = await RequestRepo.CloneItem(fromUrl, toPath);

            if (!res.IsSuccess)
                return res;

            _entryCache.OnCreate(timestampBeforeOperation,
                toPath, GetItemAsync(toPath, fastGetFromCloud: true), null);

            return res;
        }
        finally
        {
            _entryCache.UnregisterOperation(toPath);
            _entryCache.UnregisterOperation(fromUrl);
        }
    }

    public async Task<Stream> GetFileDownloadStream(File file, long? start, long? end)
    {
        var result = new DownloadStreamFabric(this).Create(file, start, end);
        var task = Task.FromResult(result).ConfigureAwait(false);
        Stream stream = await task;
        return stream;
    }


    public async Task<Stream> GetFileUploadStream(string fullFilePath, long size, Action fileStreamSent, Action serverFileProcessed, bool discardEncryption = false)
    {
        var file = new File(fullFilePath, size);

        var f = new UploadStreamFabric(this)
        {
            FileStreamSent = fileStreamSent,
            ServerFileProcessed = serverFileProcessed
        };

        var task = await Task.FromResult(f.Create(file, OnFileUploaded, discardEncryption)).ConfigureAwait(false);
        var stream = await task;

        return stream;
    }

    public event FileUploadedDelegate FileUploaded;

    private void OnFileUploaded(IEnumerable<File> files)
    {
        var lst = files.ToList();
        FileUploaded?.Invoke(lst);
    }

    public void OnBeforeUpload(string path)
    {
        _entryCache.RegisterOperation(path, CounterOperation.Upload);
    }

    public void OnAfterUpload(string path, DateTime timestampBeforeOperation)
    {
        _entryCache.OnCreate(timestampBeforeOperation, path, GetItemAsync(path, fastGetFromCloud: true), null);
        _entryCache.UnregisterOperation(path);
    }

    public T DownloadFileAsJson<T>(File file)
    {
        using var stream = RequestRepo.GetDownloadStream(file);
        using var reader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(reader);

        var ser = new JsonSerializer();
        return ser.Deserialize<T>(jsonReader);
    }

    /// <summary>
    /// Download content of file
    /// </summary>
    /// <param name="path"></param>
    /// <returns>file content or null if NotFound</returns>
    public async Task<string> DownloadFileAsString(string path)
    {
        try
        {
            var entry = await GetItemAsync(path);
            if (entry is null || entry is not File file)
                return null;
            {
                using var stream = RequestRepo.GetDownloadStream(file);
                using var reader = new StreamReader(stream);

                string res = await reader.ReadToEndAsync();
                return res;
            }
        }
        catch (Exception e)
            when (  // let's check if there really no file or just other network error
                e is AggregateException &&
                 e.InnerException is WebException we &&
                 we.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound }
                ||
                e is WebException wee &&
                 wee.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound }
            )
        {
            return null;
        }
    }


    public bool UploadFile(string path, byte[] content, bool discardEncryption = false)
    {
        DateTime timestamp = DateTime.Now;
        using var stream = GetFileUploadStream(path, content.Length, null, null, discardEncryption).Result;
        stream.Write(content, 0, content.Length);
        return true;
    }


    public bool UploadFile(string path, string content, bool discardEncryption = false)
    {
        var data = Encoding.UTF8.GetBytes(content);
        return UploadFile(path, data, discardEncryption);
    }

    public bool UploadFileJson<T>(string fullFilePath, T data, bool discardEncryption = false)
    {
        string content = JsonConvert.SerializeObject(data, Formatting.Indented);
        UploadFile(fullFilePath, content, discardEncryption);
        return true;
    }

    #region IDisposable Support
    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _entryCache?.Dispose();
            CancelToken?.Dispose();
        }
        _disposedValue = true;
    }

    public void Dispose() => Dispose(true);

    #endregion

    public async Task<bool> LinkItem(Uri url, string path, string name, bool isFile, long size, DateTime? creationDate)
    {
        if (LinkManager is null)
            return false;

        DateTime timestampBeforeOperation = DateTime.Now;

        var res = await LinkManager.Add(url, path, name, isFile, size, creationDate);
        if (res)
        {
            LinkManager.Save();
            _entryCache.OnCreate(timestampBeforeOperation, path, GetItemAsync(path, fastGetFromCloud: true), null);
        }
        return res;
    }

    public async void RemoveDeadLinks()
    {
        if (LinkManager is null)
            return;

        var count = await LinkManager.RemoveDeadLinks(true);
        if (count > 0)
            _entryCache.Clear();
    }

    public async Task<AddFileResult> AddFile(IFileHash hash, string fullFilePath, long size, ConflictResolver? conflict = null)
    {
        try
        {
            DateTime timestampBeforeOperation = DateTime.Now;
            _entryCache.RegisterOperation(fullFilePath, CounterOperation.Upload);

            var res = await RequestRepo.AddFile(fullFilePath, hash, size, DateTime.Now, conflict);

            if (!res.Success)
                return res;

            _entryCache.OnCreate(timestampBeforeOperation, fullFilePath, GetItemAsync(fullFilePath, fastGetFromCloud: true), null);

            return res;
        }
        finally
        {
            _entryCache.UnregisterOperation(fullFilePath);
        }
    }

    public async Task<AddFileResult> AddFileInCloud(File fileInfo, ConflictResolver? conflict = null)
    {
        var res = await AddFile(fileInfo.Hash, fileInfo.FullPath, fileInfo.OriginalSize, conflict);

        return res;
    }

    public async Task<bool> SetFileDateTime(File file, DateTime dateTime)
    {
        if (file.LastWriteTimeUtc == dateTime)
            return true;

        try
        {
            DateTime timestampBeforeOperation = DateTime.Now;
            _entryCache.RegisterOperation(file.FullPath, CounterOperation.Upload);

            var res = await RequestRepo.AddFile(file.FullPath, file.Hash, file.Size, dateTime, ConflictResolver.Rename);

            if (!res.Success)
                return false;

            file.LastWriteTimeUtc = dateTime;

            _entryCache.OnCreate(timestampBeforeOperation, file.FullPath, GetItemAsync(file.FullPath, fastGetFromCloud: true), null);

            return true;
        }
        finally
        {
            _entryCache.UnregisterOperation(file.FullPath);
        }
    }

    /// <summary>
    /// Создаёт в каталоге признак, что файлы в нём будут шифроваться
    /// </summary>
    /// <param name="folder"></param>
    /// <returns></returns>
    public async Task<bool> CryptInit(Folder folder)
    {
        // do not allow to crypt root path... don't know for what
        if (WebDavPath.PathEquals(folder.FullPath, WebDavPath.Root))
            return false;

        string filepath = WebDavPath.Combine(folder.FullPath, CryptFileInfo.FileName);
        var file = await GetItemAsync(filepath).ConfigureAwait(false);

        if (file is not null)
            return false;

        var content = new CryptFileInfo
        {
            Initialized = DateTime.Now
        };

        var res = UploadFileJson(filepath, content);
        return res;
    }

    public void CleanTrash()
    {
        RequestRepo.CleanTrash();
    }
}
