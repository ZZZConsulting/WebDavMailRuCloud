using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;

internal class YadAuthAskV2Request(HttpCommonSettings settings, IAuth auth, string csrf, string uid)
    : BaseRequestJson<YadAuthAskV2RequestResult>(settings, auth)
{
    protected override string RelationalUri => "/registration-validations/auth/additional_data/ask_v2";

    protected override HttpWebRequest CreateRequest(string baseDomain = null)
    {
        var request = base.CreateRequest("https://passport.yandex.ru");
        request.Referer = "https://passport.yandex.ru/";
        request.Headers["Sec-Fetch-Mode"] = "cors";
        request.Headers["Sec-Fetch-Site"] = "same-origin";

        return request;
    }

    protected override byte[] CreateHttpContent()
    {
        var keyValues = new List<KeyValuePair<string, string>>
        {
            new("csrf_token", csrf),
            new("uid", uid)
        };
        FormUrlEncodedContent z = new FormUrlEncodedContent(keyValues);
        var d = z.ReadAsByteArrayAsync().Result;
        return d;
    }
}

internal class YadAuthAskV2RequestResult
{
    public bool HasError => Status == "error";

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("errors")]
    public List<string> Errors { get; set; }
}
