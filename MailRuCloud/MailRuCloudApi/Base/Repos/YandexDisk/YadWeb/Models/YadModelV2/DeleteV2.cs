using System.Collections.Generic;
using System.Linq;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2Delete : YadModelV2
{
    public YadModelV2Delete(string path)
    {
        APIMethod = "mpfs/bulk-async-delete";
        ResultType = typeof(List<YadResponseV2OperationStatus>);
        RequestParameter = () => new YadRequestV2Operations()
        {
            Operations = [new YadRequestV2Operation() { Src = WebDavPath.Combine("/disk", path) }]
        };
    }

    public string OperationId
        => ((List<YadResponseV2OperationStatus>)ResultObject)?.FirstOrDefault()?.Oid;
}
