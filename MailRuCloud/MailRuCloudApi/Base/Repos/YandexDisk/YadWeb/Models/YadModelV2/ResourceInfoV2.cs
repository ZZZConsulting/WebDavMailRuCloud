using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2ResourceInfo : YadModelV2
{
    public YadModelV2ResourceInfo(string path)
    {
        APIMethod = "mpfs/bulk-resource-info";
        ResultType = typeof(List<FolderInfoDataResource>);
        RequestParameter = () => new YadRequestV2ResourceInfo()
        {
            Paths = [WebDavPath.Combine("/disk", path)]
        };
    }

    public FolderInfoDataResource Result
        => ((List<FolderInfoDataResource>)ResultObject)?.FirstOrDefault();
}

public class YadRequestV2ResourceInfo : YadRequestV2Parameter
{
    [JsonProperty("ids")]
    public List<string> Paths { get; set; }
}

internal class YadItemInfoRequestData : YadResponseV2Error
{
    [JsonProperty("ctime")]
    public long Ctime { get; set; }

    [JsonProperty("meta")]
    public YadItemInfoRequestMeta Meta { get; set; }

    [JsonProperty("mtime")]
    public ulong Mtime { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("utime")]
    public long Utime { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}

internal class YadItemInfoRequestMeta
{
    [JsonProperty("mimetype")]
    public string Mimetype { get; set; }

    [JsonProperty("drweb")]
    public long DrWeb { get; set; }

    [JsonProperty("resource_id")]
    public string ResourceId { get; set; }

    [JsonProperty("mediatype")]
    public string MediaType { get; set; }

    [JsonProperty("file_id")]
    public string FileId { get; set; }

    [JsonProperty("versioning_status")]
    public string VersioningStatus { get; set; }

    [JsonProperty("size")]
    public long Size { get; set; }

    [JsonProperty("short_url")]
    public string UrlShort { get; set; }

    [JsonProperty("group", NullValueHandling = NullValueHandling.Ignore)]
    public GroupInfo Group { get; set; }
}

internal class YadItemInfoRequestParams
{
    [JsonProperty("id")]
    public string Id { get; set; }
}
