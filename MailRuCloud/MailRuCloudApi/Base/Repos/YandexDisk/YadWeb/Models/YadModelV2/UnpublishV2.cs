using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2Unpublish : YadModelV2
{
    public YadModelV2Unpublish(string path)
    {
        APIMethod = "mpfs/set-public";
        ResultType = typeof(void);
        RequestParameter = () => new YadModelV2UnpublishInfo()
        {
            Path = WebDavPath.Combine("/disk", path),
            Type = "file"
        };
    }
}

public class YadModelV2UnpublishInfo : YadRequestV2Parameter
{
    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }
}
