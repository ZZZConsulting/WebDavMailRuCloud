using System.Collections.Generic;
using System.Threading;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

namespace YaR.Clouds.Base.Requests.Types;

public class CheckUpInfo
{
    public class CheckInfo
    {
        //public long FilesCount;
        //public long Free;
        //public long Trash;

        /// <summary>Набор счетчиков, возвращаемых методом cloud/virtual-disk-journal-counters</summary>
        public JournalCounters JournalCounters;
    };

    /// <summary>Набор информации для проверки изменений на диске, минуя данный сервис.</summary>
    public CheckInfo AccountInfo { get; set; }

    /// <summary>Список активных операций на сервере.</summary>
    public List<ActiveOperation> ActiveOperations { get; set; }
}

public enum CounterOperation
{
    None = 0,
    Remove,
    Rename,
    Move,
    Copy,
    Upload,
    TakeSomeonesFolder,
    NewFolder,
    //Update,
    RemoveToTrash,
    RestoreFromTrash,
    TrashDropItem,
    TrashDropAll
}

public class JournalCounters
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(JournalCounters));

    /// <summary>Удаление навсегда</summary>
    public long RemoveCounter = 0;
    public const string RemoveCounterStr = "fs-rm";

    /// <summary>Переименование</summary>
    public long RenameCounter = 0;
    public const string RenameCounterStr = "fs-rename";

    /// <summary>Перемещение</summary>
    public long MoveCounter = 0;
    public const string MoveCounterStr = "fs-move";

    /// <summary>Копирование</summary>
    public long CopyCounter = 0;
    public const string CopyCounterStr = "fs-copy";

    /// <summary>Загрузка</summary>
    public long UploadCounter = 0;
    public const string UploadCounterStr = "fs-store";

    /// <summary>Сохранение на Диск</summary>
    public long TakeSomeonesFolderCounter = 0;
    public const string TakeSomeonesFolderCounterStr = "fs-store-download";

    /// <summary>Создание папки</summary>
    public long NewFolderCounter = 0;
    public const string NewFolderCounterStr = "fs-mkdir";

    /// <summary>Изменение файла</summary>
    /*
     * Не используется, не понятно что делать с этим счетчиком.
     * При удалении файла и тут же загрузке этого же файла,
     * счетчик увеличивается. Но если править какой-нибудь
     * документ онлайн на Диске, то счетчик не меняется.
     */
    //public long UpdateCounter = 0;
    //public const string UpdateCounterStr = "fs-store-update";

    /// <summary>Удаление в Корзину</summary>
    public long RemoveToTrashCounter = 0;
    public const string RemoveToTrashCounterStr = "fs-trash-append";

    /// <summary>Удаление в Корзину</summary>
    public long RestoreFromTrashCounter = 0;
    public const string RestoreFromTrashCounterStr = "fs-trash-restore";

    /// <summary>Удаление из Корзины</summary>
    public long TrashDropItemCounter = 0;
    public const string TrashDropItemCounterStr = "fs-trash-drop";

    /// <summary>Очистка Корзины</summary>
    public long TrashDropAllCounter = 0;
    public const string TrashDropAllCounterStr = "fs-trash-drop-all";

    public JournalCounters()
    {
    }

    public JournalCounters(YadJournalCountersV2 data)
    {
        long val;
        var dict = data.EventTypes;
        RemoveCounter = dict.TryGetValue(RemoveCounterStr, out val) ? val : 0;
        RenameCounter = dict.TryGetValue(RenameCounterStr, out val) ? val : 0;
        MoveCounter = dict.TryGetValue(MoveCounterStr, out val) ? val : 0;
        CopyCounter = dict.TryGetValue(CopyCounterStr, out val) ? val : 0;
        UploadCounter = dict.TryGetValue(UploadCounterStr, out val) ? val : 0;
        TakeSomeonesFolderCounter = dict.TryGetValue(TakeSomeonesFolderCounterStr, out val) ? val : 0;
        NewFolderCounter = dict.TryGetValue(NewFolderCounterStr, out val) ? val : 0;
        //UpdateCounter = dict.TryGetValue(UpdateCounterStr, out val) ? val : 0;
        RemoveToTrashCounter = dict.TryGetValue(RemoveToTrashCounterStr, out val) ? val : 0;
        RestoreFromTrashCounter = dict.TryGetValue(RestoreFromTrashCounterStr, out val) ? val : 0;
        TrashDropItemCounter = dict.TryGetValue(TrashDropItemCounterStr, out val) ? val : 0;
        TrashDropAllCounter = dict.TryGetValue(TrashDropAllCounterStr, out val) ? val : 0;
    }

    public void Increment(CounterOperation operation)
    {
        switch (operation)
        {
        case CounterOperation.None:
            break;
        case CounterOperation.Remove:
            Interlocked.Increment(ref RemoveCounter);
            break;
        case CounterOperation.Rename:
            Interlocked.Increment(ref RenameCounter);
            break;
        case CounterOperation.Move:
            Interlocked.Increment(ref MoveCounter);
            break;
        case CounterOperation.Copy:
            Interlocked.Increment(ref CopyCounter);
            break;
        case CounterOperation.Upload:
            Interlocked.Increment(ref UploadCounter);
            //Logger.Warn($"UploadCounter->{UploadCounter}");
            break;
        case CounterOperation.TakeSomeonesFolder:
            Interlocked.Increment(ref TakeSomeonesFolderCounter);
            break;
        case CounterOperation.NewFolder:
            Interlocked.Increment(ref NewFolderCounter);
            break;
        //case CounterOperation.Update:
        //    UpdateCounter++;
        //    break;
        case CounterOperation.RemoveToTrash:
            Interlocked.Increment(ref RemoveToTrashCounter);
            //Logger.Warn($"RemoveToTrashCounter->{RemoveToTrashCounter}");
            break;
        case CounterOperation.RestoreFromTrash:
            Interlocked.Increment(ref RestoreFromTrashCounter);
            break;
        case CounterOperation.TrashDropItem:
            Interlocked.Increment(ref TrashDropItemCounter);
            break;
        case CounterOperation.TrashDropAll:
            Interlocked.Increment(ref TrashDropAllCounter);
            break;
        }
    }

    public void TakeMax(JournalCounters src)
    {
        if (RemoveCounter < src.RemoveCounter)
            Interlocked.Exchange(ref RemoveCounter, src.RemoveCounter);
        if (RenameCounter < src.RenameCounter)
            Interlocked.Exchange(ref RenameCounter, src.RenameCounter);
        if (MoveCounter < src.MoveCounter)
            Interlocked.Exchange(ref MoveCounter, src.MoveCounter);
        if (CopyCounter < src.CopyCounter)
            Interlocked.Exchange(ref CopyCounter, src.CopyCounter);
        if (UploadCounter < src.UploadCounter)
            Interlocked.Exchange(ref UploadCounter, src.UploadCounter);
        if (TakeSomeonesFolderCounter < src.TakeSomeonesFolderCounter)
            Interlocked.Exchange(ref TakeSomeonesFolderCounter, src.TakeSomeonesFolderCounter);
        if (NewFolderCounter < src.NewFolderCounter)
            Interlocked.Exchange(ref NewFolderCounter, src.NewFolderCounter);
        //if (UpdateCounter < src.UpdateCounter)
        //    Interlocked.Exchange(ref UpdateCounter, src.UpdateCounter);
        if (RemoveToTrashCounter < src.RemoveToTrashCounter)
            Interlocked.Exchange(ref RemoveToTrashCounter, src.RemoveToTrashCounter);
        if (RestoreFromTrashCounter < src.RestoreFromTrashCounter)
            Interlocked.Exchange(ref RestoreFromTrashCounter, src.RestoreFromTrashCounter);
        if (TrashDropItemCounter < src.TrashDropItemCounter)
            Interlocked.Exchange(ref TrashDropItemCounter, src.TrashDropItemCounter);
        if (TrashDropAllCounter < src.TrashDropAllCounter)
            Interlocked.Exchange(ref TrashDropAllCounter, src.TrashDropAllCounter);
    }
}
