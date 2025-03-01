using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2CreateFolder : YadModelV2
{
    public YadModelV2CreateFolder(string path)
    {
        APIMethod = "mpfs/mkdir";
        ResultType = typeof(void);
        RequestParameter = () => new YadRequestV2CreateFolder()
        {
            Path = WebDavPath.Combine("/disk", path)
        };
    }
}

public class YadRequestV2CreateFolder : YadRequestV2Parameter
{
    [JsonProperty("path")]
    public string Path { get; set; }
}
