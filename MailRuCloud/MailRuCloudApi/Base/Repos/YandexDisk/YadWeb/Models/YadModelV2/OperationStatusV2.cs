using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2OperationStatus : YadModelV2
{
    public YadModelV2OperationStatus(string opId)
    {
        APIMethod = "mpfs/bulk-operation-status";
        ResultType = typeof(Dictionary<string, YadResponseV2OperationStatus>);
        RequestParameter = () => new YadRequestV2Oids()
        {
            OperationIds = [opId]
        };
    }

    public Dictionary<string /* Oid */, YadResponseV2OperationStatus> OperationStatuses
        => (Dictionary<string /* Oid */, YadResponseV2OperationStatus>)ResultObject;
}

public class YadRequestV2Oids : YadRequestV2Parameter
{
    [JsonProperty("oids")]
    public List<string> OperationIds { get; set; }
}
