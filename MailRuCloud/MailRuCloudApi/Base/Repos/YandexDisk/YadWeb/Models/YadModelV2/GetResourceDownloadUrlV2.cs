using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2GetResourceDownloadUrl : YadModelV2
{
    public YadModelV2GetResourceDownloadUrl(string path)
    {
        APIMethod = "mpfs/url";
        ResultType = typeof(YadResponseV2GetResourceDownloadUrl);
        RequestParameter = () => new YadRequestV2GetResourceDownloadUrl()
        {
            Path = WebDavPath.Combine("/disk", path),
        };
    }

    public YadResponseV2GetResourceDownloadUrl DownloadInfo
        => (YadResponseV2GetResourceDownloadUrl)ResultObject;
}

public class YadRequestV2GetResourceDownloadUrl : YadRequestV2Parameter
{
    [JsonProperty("path")]
    public string Path { get; set; }
}

internal class YadResponseV2GetResourceDownloadUrl : YadResponseV2Error
{
    [JsonProperty("digest")]
    public string Digest { get; set; }

    [JsonProperty("file")]
    public string File { get; set; }
}
