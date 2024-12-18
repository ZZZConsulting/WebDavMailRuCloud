using System.Collections.Generic;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class JournalCountersV2 : YadModelV2
{
    public JournalCountersV2()
    {
        APIMethod = "intapi/journal-counters";
        ResultType = typeof(YadJournalCountersV2);
        RequestParameter = new YadRequestV2JournalCounters()
        {
            Hash = null,
            Offset = 0,
            Limit = 40,
            LimitPerGroup = 20,
            Text = "",
            EventType = "",
        };
    }

    public YadJournalCountersV2 Result
        => (YadJournalCountersV2)ResultObject;
}

public class YadRequestV2JournalCounters : YadRequestV2Parameter
{
    /*
    "requestParams": {
        "vd_hash": null,
        "page_load_date": "2024-09-24T20:59:59.999Z",
        "offset": 0,
        "text": "",
        "limit": 40,
        "event_type": "",
        "limit_per_group": 20,
        "counters_date": "2024-09-24T16:51:26.720Z",
        "tz_offset": -10800000
    }
     */
    [JsonProperty("vd_hash")]
    public string Hash { get; set; }

    //[JsonProperty("page_load_date")]
    //public string Date { get; set; }

    [JsonProperty("offset")]
    public int Offset { get; set; }

    [JsonProperty("limit_per_group")]
    public int LimitPerGroup { get; set; }

    [JsonProperty("limit")]
    public int Limit { get; set; }

    [JsonProperty("string")]
    public string Text { get; set; }

    [JsonProperty("event_type")]
    public string EventType { get; set; }

    //[JsonProperty("counters_date")]
    //public string CountersDate { get; set; }

    //[JsonProperty("tz_offset")]
    //public long TzOffset { get; set; }
}

public class YadJournalCountersV2
{
    /*
    {
        "eventTypes": {
            "fs-rm": 123,
            "share-remove-invite": 123,
            "album-change-cover": 123,
            "fs-set-public": 123,
            "space-promo-enlarge": 123,
            "fs-store-download": 123,
            "share-change-rights": 123,
            "album-create": 123,
            "share-invite-user": 123,
            "album-items-append": 123,
            "space-promo-reduce": 123,
            "fs-store-update": 123,
            "fs-trash-drop": 123,
            "fs-set-private": 123,
            "album-change-title": 123,
            "fs-move": 123,
            "fs-store": 123,
            "fs-mkdir": 123,
            "album-change-publicity": 123,
            "fs-trash-drop-all": 123,
            "share-leave-group": 123,
            "fs-rename": 123,
            "fs-store-photounlim": 123,
            "album-remove": 123,
            "fs-trash-restore": 123,
            "fs-trash-append": 123,
            "share-activate-invite": 123
        },
        "platforms": {
            "web": 123,
            "andr": 123,
            "rest": 123,
            "win": 123
        }
    }
    */
    [JsonProperty("eventTypes")]
    public Dictionary<string, long> EventTypes { get; set; }

    [JsonProperty("platforms")]
    public Dictionary<string, long> Platforms { get; set; }
}
