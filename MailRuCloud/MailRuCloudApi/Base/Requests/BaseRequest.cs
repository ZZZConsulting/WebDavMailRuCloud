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

            // suppressing escaping is obsolete and breaks, for example, Chinese names
            // url generated for %E2%80%8E and %E2%80%8F seems ok, but mail.ru replies error
            // https://stackoverflow.com/questions/20211496/uri-ignore-special-characters
            //var udriz = new Uri(new Uri(domain), RelationalUri, true);

#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var request = (HttpWebRequest)WebRequest.Create(uriz);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
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

            var httpRequest = CreateRequest();

            var content = CreateHttpContent();
            if (content != null)
            {
                httpRequest.Method = "POST";
                httpRequest.AllowWriteStreamBuffering = false;
                using Stream requestStream = await httpRequest.GetRequestStreamAsync().ConfigureAwait(false);
                /*
                 * The debug add the following to a watch list:
                 *      System.Text.Encoding.UTF8.GetString(content)
                 */
#if NET48
                await requestStream.WriteAsync(content, 0, content.Length).ConfigureAwait(false);
#else
                await requestStream.WriteAsync(content).ConfigureAwait(false);
#endif
                await requestStream.FlushAsync().ConfigureAwait(false);
                requestStream.Close();
            }
            try
            {
                using var response = (HttpWebResponse)await httpRequest.GetResponseAsync().ConfigureAwait(false);

                if ((int)response.StatusCode >= 500)
                {
                    throw new RequestException("Server fault")
                    {
                        StatusCode = response.StatusCode
                    };
                }

                RequestResponse<T> result;
                using (var responseStream = response.GetResponseStream())
                {
                    result = DeserializeMessage(response.Headers, Transport(responseStream));
                    responseStream.Close();
                }

                if (!result.Ok || response.StatusCode != HttpStatusCode.OK)
                {
                    var exceptionMessage =
                        $"Request failed (status code {(int)response.StatusCode}): {result.Description}";
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
                Logger.Debug($"HTTP:{httpRequest.Method}:{httpRequest.RequestUri.AbsoluteUri} ({watch.Elapsed.Milliseconds} ms)");
            }
        }

        protected abstract TConvert Transport(Stream stream);

        protected abstract RequestResponse<T> DeserializeMessage(NameValueCollection responseHeaders, TConvert data);
    }
}
