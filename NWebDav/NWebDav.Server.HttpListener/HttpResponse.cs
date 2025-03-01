using System.IO;
using System.Net;

using NWebDav.Server.Http;

namespace NWebDav.Server.HttpListener
{
    public class HttpResponse : IHttpResponse
    {
        private readonly HttpListenerResponse _response;

        private bool _isAborted;

        internal HttpResponse(HttpListenerResponse response)
        {
            _response = response;
            _isAborted = false;
        }

        public int Status
        {
            get => _response.StatusCode;
            set => _response.StatusCode = value;
        }

        public string StatusDescription
        {
            get => _response.StatusDescription;
            set => _response.StatusDescription = value;
        }

        public void SetHeaderValue(string header, string value)
        {
            switch (header)
            {
            case "Content-Length":
                _response.ContentLength64 = long.Parse(value);
                break;

            case "Content-Type":
                _response.ContentType = value;
                break;

            default:
                _response.Headers[header] = value;
                break;
            }
        }

        public void Abort()
        {
            _isAborted = true;
            _response.Abort();
        }

        public bool IsAborted => _isAborted;

        public Stream Stream => _response.OutputStream;
    }
}
