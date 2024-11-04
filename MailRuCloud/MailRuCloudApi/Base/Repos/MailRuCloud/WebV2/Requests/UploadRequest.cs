using System;
using System.Net;
using System.Runtime;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebV2.Requests;

class UploadRequest
{
    public UploadRequest(string shardUrl, File file, IAuth auth, HttpCommonSettings settings)
    {
        Request = CreateRequest(shardUrl, auth, file, settings);
    }

    public HttpWebRequest Request { get; }

    private static HttpWebRequest CreateRequest(string shardUrl, IAuth auth, File file, HttpCommonSettings settings)
    {
        var url = new Uri($"{shardUrl}?cloud_domain=2&{auth.Login}");

#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var request = (HttpWebRequest)WebRequest.Create(url.OriginalString);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
        request.Proxy = settings.Proxy;
        request.CookieContainer = auth.Cookies;
        request.Method = "PUT";
        request.ContentLength = file.OriginalSize; // + boundary.Start.LongLength + boundary.End.LongLength;
        request.Referer = $"{settings.BaseDomain}/home/{Uri.EscapeDataString(file.Path)}";
        request.Headers.Add("Origin", settings.BaseDomain);
        request.Host = url.Host;
        //request.ContentType = $"multipart/form-data; boundary=----{boundary.Guid}";
        request.Accept = "*/*";
        request.UserAgent = settings.UserAgent;
        request.AllowWriteStreamBuffering = false;
        return request;
    }

    public static implicit operator HttpWebRequest(UploadRequest v)
    {
        return v.Request;
    }
}
