using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2FolderInfo : YadModelV2
{
    public YadModelV2FolderInfo(string path, string pathPrefix = "/disk")
    {
        APIMethod = "mpfs/resources";
        ResultType = typeof(YadResponseV2FolderInfo);
        RequestParameter = () => new YadRequestV2FolderInfo()
        {
            SortBy = SortBy,
            Order = Order.ToString(),
            Path = WebDavPath.Combine(pathPrefix, path),
            Amount = Amount,
            Offset = Offset,
            WithParent = WithParent ? "1" : "0"
        };
    }

    public YadResponseV2FolderInfo FolderInfo
        => (YadResponseV2FolderInfo)ResultObject;


    /// <summary>
    /// <para>0 - сортировка возвращаемого результата по убыванию.</para>
    /// <para>1 - сортировка возвращаемого результата по возрастанию.</para>
    /// </summary>
    public int Order { get; set; } = 1;

    /// <summary>
    /// <para>Поле сортировки возвращаемого результата.</para>
    /// <para>name - по названию файла.</para>
    /// <para>mtime - по времени изменения.</para>
    /// <para>size - по размеру.</para>
    /// <para>type - по типу.</para>
    /// </summary>
    public string SortBy { get; set; } = "name";

    public int Offset { get; set; } = 0;
    public int Amount { get; set; } = int.MaxValue;
    public bool WithParent { get; set; } = false;
}

public class YadRequestV2FolderInfo : YadRequestV2Parameter
{
    [JsonProperty("sort")]
    public string SortBy { get; set; }

    [JsonProperty("order")]
    public string Order { get; set; }

    [JsonProperty("idContext")]
    public string Path { get; set; }

    [JsonProperty("amount")]
    public int Amount { get; set; }

    [JsonProperty("offset")]
    public int Offset { get; set; }

    [JsonProperty("withParent")]
    public string WithParent { get; set; }
}

internal class YadResponseV2FolderInfo : YadResponseV2Error
{
    [JsonProperty("resources")]
    public List<FolderInfoDataResource> Resources { get; set; }
}

internal class FolderInfoDataResource
{
    [JsonProperty("ctime")]
    public long Ctime { get; set; }

    [JsonProperty("meta")]
    public Meta Meta { get; set; }

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

    [JsonProperty("etime", NullValueHandling = NullValueHandling.Ignore)]
    public long? Etime { get; set; }
}

internal class Size
{
    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}

internal class VideoInfo
{
    [JsonProperty("format")]
    public string Format { get; set; }

    [JsonProperty("creationTime")]
    public long CreationTime { get; set; }

    [JsonProperty("streams")]
    public List<Stream> Streams { get; set; }

    [JsonProperty("startTime")]
    public long StartTime { get; set; }

    [JsonProperty("duration")]
    public long Duration { get; set; }

    [JsonProperty("bitRate")]
    public long BitRate { get; set; }
}

internal class Meta
{
    [JsonProperty("file_id")]
    public string FileId { get; set; }

    [JsonProperty("resource_id")]
    public string ResourceId { get; set; }

    [JsonProperty("mimetype", NullValueHandling = NullValueHandling.Ignore)]
    public string Mimetype { get; set; }

    [JsonProperty("drweb", NullValueHandling = NullValueHandling.Ignore)]
    public long? DrWeb { get; set; }

    [JsonProperty("sizes", NullValueHandling = NullValueHandling.Ignore)]
    public List<Size> Sizes { get; set; }

    [JsonProperty("mediatype", NullValueHandling = NullValueHandling.Ignore)]
    public string MediaType { get; set; }

    [JsonProperty("etime", NullValueHandling = NullValueHandling.Ignore)]
    public long? Etime { get; set; }

    [JsonProperty("versioning_status", NullValueHandling = NullValueHandling.Ignore)]
    public string VersioningStatus { get; set; }

    [JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
    public long? Size { get; set; }

    [JsonProperty("video_info", NullValueHandling = NullValueHandling.Ignore)]
    public VideoInfo VideoInfo { get; set; }

    [JsonProperty("short_url")]
    public string UrlShort { get; set; }

    [JsonProperty("total_results_count")]
    public int? TotalEntityCount { get; set; }

    [JsonProperty("group", NullValueHandling = NullValueHandling.Ignore)]
    public GroupInfo Group { get; set; }
}

internal class GroupInfo
{
    [JsonProperty("is_shared", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsShared { get; set; }
}

internal class Stream
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("frameRate", NullValueHandling = NullValueHandling.Ignore)]
    public long? FrameRate { get; set; }

    [JsonProperty("displayAspectRatio", NullValueHandling = NullValueHandling.Ignore)]
    public DisplayAspectRatio DisplayAspectRatio { get; set; }

    [JsonProperty("codec")]
    public string Codec { get; set; }

    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("bitRate")]
    public long BitRate { get; set; }

    [JsonProperty("dimension", NullValueHandling = NullValueHandling.Ignore)]
    public Dimension Dimension { get; set; }

    [JsonProperty("channelsCount", NullValueHandling = NullValueHandling.Ignore)]
    public long? ChannelsCount { get; set; }

    [JsonProperty("stereo", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Stereo { get; set; }

    [JsonProperty("sampleFrequency", NullValueHandling = NullValueHandling.Ignore)]
    public long? SampleFrequency { get; set; }
}

internal class Dimension
{
    [JsonProperty("width")]
    public long Width { get; set; }

    [JsonProperty("height")]
    public long Height { get; set; }
}

internal class DisplayAspectRatio
{
    [JsonProperty("denom")]
    public long Denom { get; set; }

    [JsonProperty("num")]
    public long Num { get; set; }
}

internal class YadFolderInfoRequestParams
{
    [JsonProperty("idContext")]
    public string IdContext { get; set; }

    [JsonProperty("order")]
    public long Order { get; set; }

    [JsonProperty("sort")]
    public string Sort { get; set; }

    [JsonProperty("offset")]
    public long Offset { get; set; }

    [JsonProperty("amount")]
    public long Amount { get; set; }
}
