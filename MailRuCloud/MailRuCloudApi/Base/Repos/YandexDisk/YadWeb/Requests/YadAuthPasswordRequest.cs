using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using Newtonsoft.Json;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;

internal class YadAuthPasswordRequest : BaseRequestJson<YadAuthPasswordRequestResult>
{
    //private readonly IAuth _auth;
    private readonly string _csrf;

    private readonly string _trackId;

    public YadAuthPasswordRequest(HttpCommonSettings settings, IAuth auth, string csrf, string trackId)
        : base(settings, auth)
    {
        //_auth = auth;
        _csrf = csrf;
        _trackId = trackId;
    }

    protected override string RelationalUri => "/registration-validations/auth/multi_step/commit_password";

    protected override HttpWebRequest CreateRequest(string baseDomain = null)
    {
        var request = base.CreateRequest("https://passport.yandex.ru");

        request.Accept = "application/json, text/javascript, */*; q=0.01";
        request.Referer = "https://passport.yandex.ru/";
        //request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";

        //request.Headers.Add("sec-ch-ua", "\" Not A; Brand\";v=\"99\", \"Chromium\";v=\"99\", \"Google Chrome\";v=\"99\"");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("sec-ch-ua-mobile", "?0");
        request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
        //request.Headers.Add("Origin", "https://passport.yandex.ru");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        request.Headers.Add("Sec-Fetch-Mode", "cors");
        request.Headers.Add("Sec-Fetch-Dest", "empty");
        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        request.Headers.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7,es;q=0.6");

        return request;
    }

    protected override byte[] CreateHttpContent()
    {
#pragma warning disable SYSLIB0013 // Type or member is obsolete
        /*
         * 29.11.2023 Поскольку ниже стоит FormUrlEncodedContent(keyValues),
         * который сам делает кодирование, указание параметров здесь
         * должно быть без Uri.EscapeUriString.
         * При Uri.EscapeUriString(_auth.Password)) сервер возвращал
         * ошибку password.not_matched даже после смены пароля с последующим
         * множественным входом с вводом капчи, а затем уже без капчи.
         * И все это до тех пор, пока пароль не был задан здесь
         * просто в виде _auth.Password, без Uri.EscapeUriString.
         */
        //var keyValues = new List<KeyValuePair<string, string>>
        //{
        //    new("csrf_token", Uri.EscapeUriString(_csrf)),
        //    new("track_id", _trackId),
        //    new("password", Uri.EscapeUriString(_auth.Password)),
        //    new("retpath", Uri.EscapeUriString("https://disk.yandex.ru/client/disk"))
        //};
        var keyValues = new List<KeyValuePair<string, string>>
        {
            new("csrf_token", _csrf),
            new("track_id", _trackId),
            new("password", _auth.Password),
            new("retpath", "https://disk.yandex.ru/client/disk")
        };
#pragma warning restore SYSLIB0013 // Type or member is obsolete
        var content = new FormUrlEncodedContent(keyValues);
        var d = content.ReadAsByteArrayAsync().Result;
        return d;
    }

    protected override RequestResponse<YadAuthPasswordRequestResult> DeserializeMessage(NameValueCollection responseHeaders, Stream stream)
    {
        var res = base.DeserializeMessage(responseHeaders, stream);

        if (res.Result.State == "auth_challenge")
            throw new AuthenticationException(
                "The account requires browser login with additional confirmation by SMS or QR code",
                // Добавление исключение данного типа является признаком чтобы попробовать аутентификацию через
                // BrowserAuthenticator, если нет явного запрета по наличию знака `!` перед логином.
                new InvalidCredentialException("Use the BrowserAuthenticator application for this account please"));

        if (res.Result.Status == "error" &&
            res.Result.Errors.Count > 0)
        {
            if (res.Result.Errors[0] == "captcha.required")
            {
                throw new AuthenticationException(
                    "Authentication failed: captcha.required",
                    // Добавление исключение данного типа является признаком чтобы попробовать аутентификацию через
                    // BrowserAuthenticator, если нет явного запрета по наличию знака `!` перед логином.
                    new InvalidCredentialException("Use the BrowserAuthenticator application for this account please"));
            }
            if (res.Result.Errors[0] == "password.not_matched")
            {
                throw new AuthenticationException(
                    "Authentication failed: password.not_matched. " +
                    "The password used to log in does not match with the main password of the account. " +
                    "Do not use 'Application Passwords' here, use the main account password only! " +
                    "In case you a sure you have used the main password, try to renew the main password.");
            }

            throw new AuthenticationException("Authentication failed: " + string.Join(", ", res.Result.Errors));
        }

        if (res.Result.State == "rfc_totp")
        {
            return res;
        }
        var uid = responseHeaders["X-Default-UID"];
        if (string.IsNullOrWhiteSpace(uid))
            throw new AuthenticationException("Cannot get X-Default-UID");
        res.Result.DefaultUid = uid;

        return res;
    }
}

internal class YadAuthPasswordRequestResult
{
    public bool HasError => Status == "error";

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("state")]
    public string State { get; set; }

    [JsonProperty("retpath")]
    public string RetPath { get; set; }

    [JsonIgnore]
    public string DefaultUid { get; set; }

    [JsonProperty("errors")]
    public List<string> Errors { get; set; }
}
