using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;

namespace YaR.Clouds.Base.Streams;

internal class HttpClientFabric
{
    public static HttpClientFabric Instance => _instance ??= new HttpClientFabric();
    private static HttpClientFabric _instance;

    public HttpClient this[Cloud cloud]
    {
        get
        {
            var cli = _lockDict.GetOrAdd(cloud.Credentials, new HttpClient(new HttpClientHandler
            {
                UseProxy = true,
                Proxy = cloud.Settings.Proxy,
                CookieContainer = cloud.RequestRepo.Auth.Cookies,
                UseCookies = true,
                AllowAutoRedirect = true,
                MaxConnectionsPerServer = int.MaxValue
            })
            { Timeout = Timeout.InfiniteTimeSpan });

            return cli;
        }
    }

    private readonly ConcurrentDictionary<Credentials, HttpClient> _lockDict = new();
}
