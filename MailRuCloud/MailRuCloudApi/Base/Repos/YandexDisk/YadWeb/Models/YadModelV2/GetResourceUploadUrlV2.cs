using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2GetResourceUploadUrl : YadModelV2
{
    public YadModelV2GetResourceUploadUrl(string path, long size, string hashSha256, string hashMd5, bool force = true)
    {
        APIMethod = "mpfs/store";
        ResultType = typeof(YadResponseV2GetResourceUploadUrl);
        RequestParameter = () => new YadRequestV2GetResourceUploadUrl()
        {
            Path = WebDavPath.Combine("/disk", path),
            Force = force ? 1 : 0,
            Size = size,
            Md5 = hashMd5,
            Sha256 = hashSha256
        };
    }

    public YadResponseV2GetResourceUploadUrl UploadInfo
        => (YadResponseV2GetResourceUploadUrl)ResultObject;
}

public class YadRequestV2GetResourceUploadUrl : YadRequestV2Parameter
{
    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("force")]
    public int Force { get; set; }

    [JsonProperty("size")]
    public long Size { get; set; }

    [JsonProperty("md5")]
    public string Md5 { get; set; }

    [JsonProperty("sha256")]
    public string Sha256 { get; set; }
}

internal class YadResponseV2GetResourceUploadUrl : YadResponseV2Error
{
    [JsonProperty("at_version")]
    public long AtVersion { get; set; }

    [JsonProperty("upload_url")]
    public string Url { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("oid")]
    public string Oid { get; set; }
}
