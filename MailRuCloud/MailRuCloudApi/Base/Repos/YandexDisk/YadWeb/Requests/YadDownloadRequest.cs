using System.Net;
using System.Net.Mime;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;

internal class YadDownloadRequest(HttpCommonSettings settings, IAuth auth, string url, long instart, long inend)
{
    public HttpWebRequest Request { get; } = CreateRequest(auth, settings.Proxy, url, instart, inend, settings.UserAgent);

    private static HttpWebRequest CreateRequest(IAuth auth, IWebProxy proxy, string url, long instart, long inend, string userAgent)
    {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var request = (HttpWebRequest)WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete

        request.Headers.Add("Accept-Ranges", "bytes");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");
        request.Headers.Add("Sec-Fetch-Mode", "nested-navigate");

        request.AddRange(instart, inend);
        request.Proxy = proxy;
        request.CookieContainer = auth.Cookies;
        request.Method = "GET";
        request.ContentType = MediaTypeNames.Application.Octet;
        request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3";
        request.UserAgent = userAgent;
        request.AllowReadStreamBuffering = false;
        request.AllowAutoRedirect = true;
        request.Referer = "https://disk.yandex.ru/client/disk";

        return request;
    }

    public static implicit operator HttpWebRequest(YadDownloadRequest v)
    {
        return v.Request;
    }
}
