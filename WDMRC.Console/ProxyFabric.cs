﻿using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using MihaZupan;

namespace YaR.Clouds.Console
{
    class ProxyFabric
    {
        public static IWebProxy Get(string proxyAddress, string proxyUser, string proxyPassword)
        {
            if (string.IsNullOrEmpty(proxyAddress))
#if NET48
                return WebRequest.DefaultWebProxy;
#else
                return HttpClient.DefaultProxy;
#endif



            var match = Regex.Match(proxyAddress, @"\A\s*(?<type>(socks|https|http)) :// (?<address>\S*?) : (?<port>\d+) \s* \Z", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
            if (!match.Success)
                throw new ArgumentException("Not supported proxy type");

            string type = match.Groups["type"].Value;
            string address = match.Groups["address"].Value;
            int port = int.Parse(match.Groups["port"].Value);

            var proxy = type == "socks"
                ? string.IsNullOrEmpty(proxyUser)
                    ? (IWebProxy)new HttpToSocks5Proxy(address, port)
                    : new HttpToSocks5Proxy(address, port, proxyUser, proxyPassword)
                : new WebProxy(new Uri(proxyAddress))
                {
                    UseDefaultCredentials = string.IsNullOrEmpty(proxyUser),
                    Credentials = new NetworkCredential {UserName = proxyUser, Password = proxyPassword}
                };

            return proxy;
        }
    }
}
