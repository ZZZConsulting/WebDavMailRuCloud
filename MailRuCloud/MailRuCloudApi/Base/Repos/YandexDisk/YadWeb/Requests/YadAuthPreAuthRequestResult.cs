using System.Collections.Specialized;
using System.Net;
using System.Text.RegularExpressions;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;

internal partial class YadPreAuthRequest : BaseRequestString<YadAuthPreAuthRequestResult>
{
    public YadPreAuthRequest(HttpCommonSettings settings, IAuth auth)
        : base(settings, auth)
    {
    }

    protected override HttpWebRequest CreateRequest(string baseDomain)
    {
        var request = base.CreateRequest("https://passport.yandex.ru");
        return request;
    }

    protected override string RelationalUri => "/auth";

    //protected override byte[] CreateHttpContent()
    //{
    //    string data = $"Login={Uri.EscapeUriString(Auth.Login)}&Domain={CommonSettings.Domain}&Password={Uri.EscapeUriString(Auth.Password)}";

    //    return Encoding.UTF8.GetBytes(data);
    //}

    private const string UuidRegexMask = @"""process_uuid"":""(?<uuid>.*?)""";
    private const string CsrfRegexMask = @"""csrf"":""(?<csrf>.*?)""";
#if NET7_0_OR_GREATER
    [GeneratedRegex(UuidRegexMask)]
    private static partial Regex UuidRegex();
    private static readonly Regex s_uuidRegex = UuidRegex();

    [GeneratedRegex(CsrfRegexMask)]
    private static partial Regex CsrfRegex();
    private static readonly Regex s_csrfRegex = CsrfRegex();
#else
    private static readonly Regex s_uuidRegex = new(UuidRegexMask, RegexOptions.Compiled);
    private static readonly Regex s_csrfRegex = new(CsrfRegexMask, RegexOptions.Compiled);
#endif


    protected override RequestResponse<YadAuthPreAuthRequestResult> DeserializeMessage(NameValueCollection responseHeaders, string responseText)
    {
        var matchCsrf = s_csrfRegex.Match(responseText);
        var matchUuid = s_uuidRegex.Match(responseText);
        //var matchUuid = Regex.Match(responseText, @"process_uuid(?<uuid>\S+?)&quot;");

        var msg = new RequestResponse<YadAuthPreAuthRequestResult>
        {
            Ok = matchCsrf.Success && matchUuid.Success,
            Result = new YadAuthPreAuthRequestResult
            {
                Csrf = matchCsrf.Success ? matchCsrf.Groups["csrf"].Value : string.Empty,
                ProcessUuid = matchUuid.Success ? matchUuid.Groups["uuid"].Value : string.Empty
            }
        };

        return msg;
    }
}

internal class YadAuthPreAuthRequestResult
{
    public string Csrf { get; set; }
    public string ProcessUuid { get; set; }
}
