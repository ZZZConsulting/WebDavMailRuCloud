using System;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebBin.Requests;

class DownloadRequest
{
    public DownloadRequest(HttpCommonSettings settings, IAuth auth,
        File file, long inStart, long inEnd, string downServerUrl, IEnumerable<string> publicBaseUrls)
    {
        Request = CreateRequest(settings, auth, file, inStart, inEnd, downServerUrl, publicBaseUrls);
    }

    public HttpWebRequest Request { get; }

    private static HttpWebRequest CreateRequest(HttpCommonSettings settings,
        IAuth auth, File file, long instart, long inend, string downServerUrl, IEnumerable<string> publicBaseUrls)
    //(IAuth authenticator, IWebProxy proxy, string url, long inStart, long inEnd,  string userAgent)
    {
        bool isLinked = !file.PublicLinks.IsEmpty;

        string url;

        if (isLinked)
        {
            var urii = file.PublicLinks.Values.FirstOrDefault()?.Uri;
            var uriistr = urii?.OriginalString;
            var baseura = uriistr == null
                ? null
                : publicBaseUrls.FirstOrDefault(pbu => uriistr.StartsWith(pbu, StringComparison.InvariantCulture));
            if (string.IsNullOrEmpty(baseura))
                throw new ArgumentException("URL does not starts with base URL");

            url = $"{downServerUrl}{WebDavPath.EscapeDataString(uriistr.Remove(0, baseura.Length))}";
        }
        else
        {
            url = $"{downServerUrl}{Uri.EscapeDataString(file.FullPath.TrimStart('/'))}";
            url += $"?client_id={settings.ClientId}&token={auth.AccessToken}";
        }

        var uri = new Uri(url);

#pragma warning disable SYSLIB0014 // Type or member is obsolete
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri.OriginalString);
#pragma warning restore SYSLIB0014 // Type or member is obsolete

        request.AllowAutoRedirect = true;

        request.AddRange(instart, inend);
        request.Proxy = settings.Proxy;
        //request.CookieContainer = authenticator.Cookies;
        request.Method = "GET";
        //request.Accept = "*/*";
        //request.UserAgent = settings.UserAgent;
        //request.Host = uri.Host;
        request.AllowWriteStreamBuffering = false;

        if (isLinked)
            request.Headers.Add("Accept-Ranges", "bytes");

        request.Timeout = 15 * 1000;
        request.ReadWriteTimeout = 15 * 1000;

        return request;
    }

    public static implicit operator HttpWebRequest(DownloadRequest v)
    {
        return v.Request;
    }
}
