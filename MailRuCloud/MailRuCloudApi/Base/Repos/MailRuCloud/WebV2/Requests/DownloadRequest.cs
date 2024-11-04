using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mime;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Common;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebV2.Requests;

class DownloadRequest
{
    public DownloadRequest(File file, long inStart, long inEnd, IAuth auth, HttpCommonSettings settings, Cached<Dictionary<ShardType, ShardInfo>> shards)
    {
        Request = CreateRequest(auth, settings.Proxy, file, inStart, inEnd, settings.UserAgent, shards);
    }

    public HttpWebRequest Request { get; }

    private static HttpWebRequest CreateRequest(IAuth auth, IWebProxy proxy, File file, long instart, long inend, string userAgent, Cached<Dictionary<ShardType, ShardInfo>> shards)
    {
        bool isLinked = !file.PublicLinks.IsEmpty;

        string downloadkey = isLinked
            ? auth.DownloadToken
            : auth.AccessToken;

        var shard = isLinked
            ? shards.Value[ShardType.WeblinkGet]
            : shards.Value[ShardType.Get];

        string url = !isLinked
            ? $"{shard.Url}{Uri.EscapeDataString(file.FullPath)}"
            : $"{shard.Url}{file.PublicLinks.Values.FirstOrDefault()?.Uri.PathAndQuery.Remove(0, "/public".Length) ?? string.Empty}?key={downloadkey}";

#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var request = (HttpWebRequest)WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete

        request.Headers.Add("Accept-Ranges", "bytes");
        request.AddRange(instart, inend);
        request.Proxy = proxy;
        request.CookieContainer = auth.Cookies;
        request.Method = "GET";
        request.ContentType = MediaTypeNames.Application.Octet;
        request.Accept = "*/*";
        request.UserAgent = userAgent;
        request.AllowReadStreamBuffering = false;

        return request;
    }

    public static implicit operator HttpWebRequest(DownloadRequest v)
    {
        return v.Request;
    }
}
