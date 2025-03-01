using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;

internal class YadCommonRequest : BaseRequestJson<YadResponseResult>
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadCommonRequest));

    private readonly YadPostData _postData = new();

    private readonly List<object> _outData = [];

    private YadWebAuth YadAuth { get; }

    public YadCommonRequest(HttpCommonSettings settings, YadWebAuth auth) : base(settings, auth)
    {
        YadAuth = auth;
    }

    protected override HttpWebRequest CreateRequest(string baseDomain = null)
    {
        var request = base.CreateRequest("https://disk.yandex.ru");
        request.Referer = "https://disk.yandex.ru/client/disk";
        return request;
    }

    protected override byte[] CreateHttpContent()
    {
        _postData.Sk = YadAuth.DiskSk;
        _postData.IdClient = YadAuth.Uuid;

        return _postData.CreateHttpContent();
    }

    public YadCommonRequest With<T, TOut>(T model, out TOut resOut)
        where T : YadPostModel
        where TOut : YadResponseModel, new()
    {
        _postData.Models.Add(model);
        _outData.Add(resOut = new TOut());

        return this;
    }

    protected override string RelationalUri
        => string.Concat(
            "/models/?_m=",
            _postData
                .Models
                .Select(m => m.Name)
                .Aggregate((current, next) => current + "," + next));

    protected override RequestResponse<YadResponseResult> DeserializeMessage(
        NameValueCollection responseHeaders, System.IO.Stream stream)
    {
        using var sr = new StreamReader(stream);

        string text = sr.ReadToEnd();
        //Logger.Debug(text);

        var msg = new RequestResponse<YadResponseResult>
        {
            Ok = true,
            Result = JsonConvert.DeserializeObject<YadResponseResult>(text, new KnownYadModelConverter(_outData))
        };

        if (YadAuth.Credentials.AuthenticationUsingBrowser)
        {
            //Logger.Debug($"_postData.Sk={_postData?.Sk} | Result.sk={msg.Result?.Sk}");
            /*
             * Строка sk выглядит так: "sk": "cdc3dee74a379c1adc792ef087cf8c9ba19ca9f5:1693681795"
             * Правая часть содержит время после двоеточия - количество секунд, начиная с 01.01.1970.
             * Обновляем sk полученным значением sk.
             */
            if (!string.IsNullOrWhiteSpace(msg.Result?.Sk))
                YadAuth.DiskSk = msg.Result.Sk;

            if (msg.Result.Models != null &&
                msg.Result.Models.Any(m => m.Error != null))
            {
                Logger.Debug(text);
            }
            if (_postData.Models != null &&
                _postData.Models.Count > 0 &&
                _postData.Models[0].Name == "space")
            {
                Logger.Warn($"Yandex has API version {msg.Result.Version}");
            }
        }

        return msg;
    }
}
