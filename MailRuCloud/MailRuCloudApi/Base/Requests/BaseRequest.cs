﻿using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos;
using YaR.Clouds.Base.Repos.MailRuCloud;

namespace YaR.Clouds.Base.Requests
{
    internal abstract class BaseRequest<TConvert, T> where T : class
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(BaseRequest<TConvert, T>));

        protected readonly HttpCommonSettings Settings;
        protected readonly IAuth Auth;

        protected BaseRequest(HttpCommonSettings settings, IAuth auth)
        {
            Settings = settings;
            Auth = auth;
        }

        protected abstract string RelationalUri { get; }

        protected virtual HttpWebRequest CreateRequest(string baseDomain = null)
        {
            string domain = string.IsNullOrEmpty(baseDomain) ? ConstSettings.CloudDomain : baseDomain;
            var uriz = new Uri(new Uri(domain), RelationalUri);
            
            // supressing escaping is obsolete and breaks, for example, chinese names
            // url generated for %E2%80%8E and %E2%80%8F seems ok, but mail.ru replies error
            // https://stackoverflow.com/questions/20211496/uri-ignore-special-characters
            //var udriz = new Uri(new Uri(domain), RelationalUri, true);

            var request = (HttpWebRequest)WebRequest.Create(uriz);
            request.Proxy = Settings.Proxy;
            request.CookieContainer = Auth?.Cookies;
            request.Method = "GET";
            request.ContentType = ConstSettings.DefaultRequestType;
            request.Accept = "application/json";
            request.UserAgent = Settings.UserAgent;
            request.ContinueTimeout = Settings.CloudSettings.Wait100ContinueTimeoutMs;
            request.Timeout = Settings.CloudSettings.WaitResponseTimeoutMs;
            request.ReadWriteTimeout = Settings.CloudSettings.ReadWriteTimeoutMs;


#if NET48
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
#else
            request.AutomaticDecompression = DecompressionMethods.All;
#endif

            //request.AllowReadStreamBuffering = true;
            
            return request;
        }

        protected virtual byte[] CreateHttpContent()
        {
            return null;
        }


        public virtual async Task<T> MakeRequestAsync()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            var httprequest = CreateRequest();

            var content = CreateHttpContent();
            if (content != null)
            {
                httprequest.Method = "POST";
                var stream = httprequest.GetRequestStream();
                /*
                 * For debug add to Watch:
                 *      System.Text.Encoding.UTF8.GetString(content)
                 */
                await stream.WriteAsync(content, 0, content.Length);
            }
            try
            {
                using var response = (HttpWebResponse)await httprequest.GetResponseAsync();

                if ((int) response.StatusCode >= 500)
                    throw new RequestException("Server fault")
                    {
                        StatusCode = response.StatusCode
                    };

                RequestResponse<T> result;
#if NET48
                using (var responseStream = response.GetResponseStream())
#else
                await using (var responseStream = response.GetResponseStream())
#endif

                {
                    result = DeserializeMessage(response.Headers, Transport(responseStream));
                }

                if (!result.Ok || response.StatusCode != HttpStatusCode.OK)
                {
                    var exceptionMessage =
                        $"Request failed (status code {(int) response.StatusCode}): {result.Description}";
                    throw new RequestException(exceptionMessage)
                    {
                        StatusCode = response.StatusCode,
                        ResponseBody = string.Empty,
                        Description = result.Description,
                        ErrorCode = result.ErrorCode
                    };
                }
                var retVal = result.Result;

                return retVal;
            }
            // ReSharper disable once RedundantCatchClause
            #pragma warning disable 168
            catch (Exception ex)
            #pragma warning restore 168
            {
                throw;
            }
            finally
            {
                watch.Stop();
                Logger.Debug($"HTTP:{httprequest.Method}:{httprequest.RequestUri.AbsoluteUri} ({watch.Elapsed.Milliseconds} ms)");
            }


        }

        protected abstract TConvert Transport(Stream stream);

        protected abstract RequestResponse<T> DeserializeMessage(NameValueCollection responseHeaders, TConvert data);
    }
}
