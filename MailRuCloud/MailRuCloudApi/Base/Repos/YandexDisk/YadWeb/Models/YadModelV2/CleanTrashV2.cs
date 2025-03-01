using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2CleanTrash : YadModelV2
{
    public YadModelV2CleanTrash()
    {
        APIMethod = "mpfs/async-trash-drop-all";
        ResultType = typeof(YadResponseV2OperationStatus);
    }

    public YadResponseV2OperationStatus CleanTrashInfo
        => (YadResponseV2OperationStatus)ResultObject;
}
