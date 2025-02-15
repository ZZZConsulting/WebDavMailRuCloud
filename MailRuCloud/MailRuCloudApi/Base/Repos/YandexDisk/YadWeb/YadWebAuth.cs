﻿using System;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb
{
    class YadWebAuth : IAuth
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadWebAuth));

        public YadWebAuth(SemaphoreSlim connectionLimiter, HttpCommonSettings settings, Credentials credentials)
        {
            _settings = settings;
            Credentials = credentials;

            if (credentials?.Cookies?.Count > 0 &&
                !string.IsNullOrEmpty(credentials.Sk) &&
                !string.IsNullOrEmpty(credentials.Uuid))
            {
                Cookies = credentials.Cookies;
                Uuid = credentials.Uuid;
                DiskSk = credentials.Sk;
            }
            else
            {
                Cookies = new CookieContainer();

                var _ = MakeLogin(connectionLimiter).Result;
            }
        }

        public Credentials Credentials { get; }

        private readonly HttpCommonSettings _settings;

        public async Task<bool> MakeLogin(SemaphoreSlim connectionLimiter)
        {
            var preAuthResult = await new YadPreAuthRequest(_settings, this)
                .MakeRequestAsync(connectionLimiter);
            if (string.IsNullOrWhiteSpace(preAuthResult.Csrf))
                throw new AuthenticationException($"{nameof(YadPreAuthRequest)} error parsing csrf");
            if (string.IsNullOrWhiteSpace(preAuthResult.ProcessUuid))
                throw new AuthenticationException($"{nameof(YadPreAuthRequest)} error parsing ProcessUUID");

            Uuid = preAuthResult.ProcessUuid;

            var loginAuth = await new YadAuthLoginRequest(_settings, this, preAuthResult.Csrf, preAuthResult.ProcessUuid)
                    .MakeRequestAsync(connectionLimiter);
            if (loginAuth.HasError)
                throw new AuthenticationException($"{nameof(YadAuthLoginRequest)} error");

            var passwdAuth = await new YadAuthPasswordRequest(_settings, this, preAuthResult.Csrf, loginAuth.TrackId)
                .MakeRequestAsync(connectionLimiter);
            if (passwdAuth.HasError)
                throw new AuthenticationException($"{nameof(YadAuthPasswordRequest)} errors: {passwdAuth.Errors.Aggregate((f,s) => f + "," + s)}");


            var accsAuth = await new YadAuthAccountsRequest(_settings, this, preAuthResult.Csrf)
                .MakeRequestAsync(connectionLimiter);
            if (accsAuth.HasError)
                throw new AuthenticationException($"{nameof(YadAuthAccountsRequest)} error");

            var askv2 = await new YadAuthAskV2Request(_settings, this,  accsAuth.Csrf, passwdAuth.DefaultUid)
                .MakeRequestAsync(connectionLimiter);
            if (accsAuth.HasError)
                throw new AuthenticationException($"{nameof(YadAuthAskV2Request)} error");

            var skReq = await new YadAuthDiskSkRequest(_settings, this)
                .MakeRequestAsync(connectionLimiter);
            if (skReq.HasError)
                throw new AuthenticationException($"{nameof(YadAuthDiskSkRequest)} error, response: {skReq.HtmlResponse}");
            DiskSk = skReq.DiskSk;

            return true;
        }

        public string Login => Credentials.Login;
        public string Password => Credentials.Password;
        public string DiskSk { get; set; }
        public string Uuid { get; set; }

        public bool IsAnonymous => false;
        public string AccessToken { get; }
        public string DownloadToken { get; }
        public CookieContainer Cookies { get; }
        public void ExpireDownloadToken()
        {
            throw new NotImplementedException();
        }
    }
}
