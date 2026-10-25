using System.Net;
using System.Threading.Tasks;
using NWebDav.Server.Http;

namespace NWebDav.Server.HttpListener
{
    public abstract class HttpBaseContext : IHttpContext
    {
        private readonly HttpListenerResponse _response;

        protected HttpBaseContext(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Assign properties
            Request = new HttpRequest(request);
            response.SendChunked = true;
            Response = new HttpResponse(response);

            // Save response
            _response = response;
        }

        public IHttpRequest Request { get; }
        public IHttpResponse Response { get; }
        public abstract IHttpSession Session { get; }

        public Task CloseAsync()
        {
            if (_response != null)
            {
                // Prevent any exceptions
                try
                {
                    if (!Response.IsAborted)
                    {
                        // At first send remaining buffered byte to client
                        /*
                         * По идее метод _response.Close() должен сам закрывать OutputStream.
                         * В NET 9 здесь ошибка System.InvalidOperationException: Cannot close stream until all bytes are written
                         * Причина в отправке Content-Length с заданным числом и фактической отправке в поток меньшего числа байтов,
                         * по разным причинам - ошибка облачного сервера и т.д.
                         * Рекомендация была - не делать Flush и Close принудительно.
                         * Но помогла переделка кода в GetAndHeadHandler.cs - добавлена проверка, что потом от сервера ничего не дал,
                         * и в таком случае делается response.Abort() вместо обычного завершения записи нуля байт.
                         */
                        _response.OutputStream?.Flush();
                        _response.OutputStream?.Close();
                    }
                }
                catch { }

                try
                {
                    // Then close the response
                    /*
                     * Продолжение ошибки
                     * System.InvalidOperationException: Cannot close stream until all bytes are written
                     * См. комментарий выше.
                     */
                    _response.Close();
                }
                catch { }
            }

            // Command completed synchronous
            return Task.FromResult(true);
        }
    }
}
