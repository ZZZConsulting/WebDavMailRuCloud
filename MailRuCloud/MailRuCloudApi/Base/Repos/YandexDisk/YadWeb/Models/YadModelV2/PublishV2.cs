using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2Publish : YadModelV2
{
    public YadModelV2Publish(string path)
    {
        APIMethod = "mpfs/set-public";
        ResultType = typeof(YadResponseV2Publish);
        RequestParameter = () => new YadModelV2PublishInfo()
        {
            AllowDefaultSettingsAvailable = true,
            Path = WebDavPath.Combine("/disk", path),
            Type = "resource"
        };
    }

    public YadResponseV2Publish PublishInfo
        => (YadResponseV2Publish)ResultObject;
}

public class YadModelV2PublishInfo : YadRequestV2Parameter
{
    [JsonProperty("allowDefaultSettingsAvailable")]
    public bool AllowDefaultSettingsAvailable;

    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }
}

public class YadResponseV2Publish : YadResponseV2Error
{
    // like "url": "https://disk.yandex.net/disk/public/?hash=XTRXLyw1qVu91b7cfP6JkNL1nXjER9wy19nkQcw38e12BrYmOI5P1IBe9qaz1Yjks1q/J6bpm1yOJonT3VoXn1ag%3D%3D"
    [JsonProperty("url")]
    public string Url { get; set; }

    // like "short_url": "https://yadi.sk/d/AQDE6QUv0XQDTQ"
    [JsonProperty("short_url")]
    public string ShortUrl { get; set; }

    // like "hash": "1TQXLyw1qVQ9Ob7QQP6JkNL9nXjQR9wyQ9nkQcw38e+rYmOIQP5IBe9qazuYjksvq/JQbpmRyOJo1TQVoXnDag=="
    [JsonProperty("hash")]
    public string Hash { get; set; }

    // like "short_url_named": "https://yadi.sk/d/AQAQ6RUvQXfDQQ?file"
    [JsonProperty("short_url_named")]
    public string ShortUrlNamed { get; set; }
}
