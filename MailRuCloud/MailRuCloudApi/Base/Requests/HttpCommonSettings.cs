using System.Net;

namespace YaR.Clouds.Base.Requests
{
    public class HttpCommonSettings
    {
        public IWebProxy Proxy { get; set; }
        public string ClientId { get; set; }
        public string UserAgent { get; set; }

        public string CloudDomain { get; set; }
        public string RequestContentType { get; set; }
    }
}