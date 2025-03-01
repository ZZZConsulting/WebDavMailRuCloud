using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2ActiveOperationsV2 : YadModelV2
{
    public YadModelV2ActiveOperationsV2()
    {
        APIMethod = "mpfs/active-operations";
        ResultType = typeof(List<YadResponseV2ActiveOperationStatus>);
    }

    /// <summary>Result of operation.</summary>
    public List<YadResponseV2ActiveOperationStatus> ActiveOperations
        => ((List<YadResponseV2ActiveOperationStatus>)ResultObject);
}

internal class YadResponseV2ActiveOperationStatus : YadResponseV2Error
{
    [JsonProperty("ycrid")]
    public string Ycrid { get; set; }

    [JsonProperty("ctime")]
    public long Ctime { get; set; }

    [JsonProperty("data")]
    public YadResponseV2ActiveOperationStatusResult Data { get; set; }

    [JsonProperty("dtime")]
    public long Dtime { get; set; }

    /// <summary>
    /// Подтип операции, например, "bulk-download-prepare"
    /// </summary>
    [JsonProperty("subtype")]
    public string Subtype { get; set; }

    /// <summary>
    /// Число, например, 1
    /// </summary>
    [JsonProperty("state")]
    public int State { get; set; }

    [JsonProperty("mtime")]
    public long Mtime { get; set; }

    [JsonProperty("md5")]
    public string Md5 { get; set; }

    /// <summary>
    /// Тип операции, например 'move' или 'download'
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; }

    /// <summary>
    /// Это идентификатор для передачи параметром в метод <see cref="YadWebRequestRepo.WaitForOperation"/>
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; }

    /// <summary>
    /// ID пользователя, запустившего операцию
    /// </summary>
    [JsonProperty("uid")]
    public long Uid { get; set; }
}

internal class YadResponseV2ActiveOperationStatusResult : YadModelDataBase
{
    [JsonProperty("items")]
    public Dictionary<string /*uid*/, List<string> /*64 hex symbols*/> Items { get; set; }

    [JsonProperty("at_version")]
    public long AtVersion { get; set; }

    /// <summary>
    /// Пример: "download_url": "https://downloader.disk.yandex.ru/zip-files/123456789091818da9e14c2cf399a8de39806f9ad3b9f424266cbc4ae49139e1/67a1e263/NWYxNTgyODgzOGYxODQ4MGU3MTA2OWM1OWFjMTNkNzk2NWFiMjM1YzY4ZmZlYTQ0ZGU5ZmJhZDE3Njk4N2RlOQ==?uid=938070248&filename=archive-2025-02-04_08-48-19.zip&disposition=attachment&hash=5f15828838f18480e71069c59ac13d7965ab235c68ffea44de9fbad176987de9&limit=0&owner_uid=123456789&tknv=v2"
    /// </summary>
    [JsonProperty("download_url")]
    public string DownloadUrl { get; set; }

    /// <summary>
    /// Пример: "target": "12-it's-uid-34:/disk/destination-folder"
    /// </summary>
    [JsonProperty("target")]
    public string Target { get; set; }

    /// <summary>
    /// Пример: "source": "12-it's-uid-34:/disk/source-folder"
    /// </summary>
    [JsonProperty("source")]
    public string Source { get; set; }
}
