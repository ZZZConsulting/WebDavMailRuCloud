using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2AccountInfo : YadModelV2
{
    public YadModelV2AccountInfo()
    {
        APIMethod = "mpfs/space";
        ResultType = typeof(YadResponseV2AccountInfo);
    }

    /// <summary>Result of operation.</summary>
    public YadResponseV2AccountInfo AccountInfo
        => (YadResponseV2AccountInfo)ResultObject;
}

public class YadResponseV2AccountInfo : YadResponseV2Error
{
    [JsonProperty("files_count")]
    public long FilesCount { get; set; }

    [JsonProperty("used")]
    public long Used { get; set; }

    [JsonProperty("limit")]
    public long Limit { get; set; }

    [JsonProperty("uid")]
    public string Uid { get; set; }

    [JsonProperty("filesize_limit")]
    public long FileSizeLimit { get; set; }

    [JsonProperty("trash")]
    public long Trash { get; set; }

    [JsonProperty("free")]
    public long Free { get; set; }

    [JsonProperty("photounlim")]
    public int PhotoUnlim { get; set; }
}
