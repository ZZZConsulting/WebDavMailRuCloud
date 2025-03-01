using System.Net;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;

internal class YadUploadRequest(HttpCommonSettings settings, YadWebAuth authenticator, string url, long size)
{
    public HttpWebRequest Request { get; } = CreateRequest(url, authenticator, size, settings);

    private static HttpWebRequest CreateRequest(string url, YadWebAuth authenticator, long size, HttpCommonSettings settings)
    {
#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var request = (HttpWebRequest)WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
        request.Proxy = settings.Proxy;
        request.CookieContainer = authenticator.Cookies;
        request.Method = "PUT";
        request.ContentLength = size;
        request.Referer = "https://disk.yandex.ru/client/disk";
        request.Headers.Add("Origin", settings.BaseDomain);
        request.Accept = "*/*";
        request.UserAgent = settings.UserAgent;
        request.AllowWriteStreamBuffering = false;
        return request;
    }

    public static implicit operator HttpWebRequest(YadUploadRequest v)
    {
        return v.Request;
    }
}
