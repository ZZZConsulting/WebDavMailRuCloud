﻿using System;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud.WebV2.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Common;
using static YaR.Clouds.Cloud;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebV2;

class WebAuth : IAuth
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(WebAuth));

    private readonly SemaphoreSlim _connectionLimiter;
    public CookieContainer Cookies { get; }

    private readonly HttpCommonSettings _settings;
    private readonly IBasicCredentials _creds;

    public WebAuth(SemaphoreSlim connectionLimiter, HttpCommonSettings settings,
        IBasicCredentials credentials, AuthCodeRequiredDelegate onAuthCodeRequired)
    {
        _connectionLimiter = connectionLimiter;
        _settings = settings;
        _creds = credentials;
        Cookies = new CookieContainer();

        var logged = MakeLogin(connectionLimiter, onAuthCodeRequired).Result;
        if (!logged)
            throw new AuthenticationException($"Cannot log in {credentials.Login}");


        _authToken = new Cached<AuthTokenResult>(_ =>
            {
                Logger.Debug("AuthToken expired, refreshing.");
                if (credentials.IsAnonymous)
                    return null;

                var token = Auth(connectionLimiter).Result;
                return token;
            },
            _ => TimeSpan.FromSeconds(AuthTokenExpiresInSec));

        _cachedDownloadToken =
            new Cached<string>(_ => new DownloadTokenRequest(_settings, this)
                                        .MakeRequestAsync(_connectionLimiter).Result.ToToken(),
                               _ => TimeSpan.FromSeconds(DownloadTokenExpiresSec));

    }

    public async Task<bool> MakeLogin(SemaphoreSlim connectionLimiter, AuthCodeRequiredDelegate onAuthCodeRequired)
    {
        var loginResult = await new LoginRequest(_settings, this)
            .MakeRequestAsync(connectionLimiter);

        // 2FA
        if (!string.IsNullOrEmpty(loginResult.Csrf))
        {
            string authCode = onAuthCodeRequired(_creds.Login, false);
            await new SecondStepAuthRequest(_settings, loginResult.Csrf, authCode)
                .MakeRequestAsync(connectionLimiter);
        }

        await new EnsureSdcCookieRequest(_settings, this)
            .MakeRequestAsync(connectionLimiter);

        return true;
    }

    public async Task<AuthTokenResult> Auth(SemaphoreSlim connectionLimiter)
    {
        var req = await new AuthTokenRequest(_settings, this).MakeRequestAsync(connectionLimiter);
        var res = req.ToAuthTokenResult();
        return res;
    }

    /// <summary>
    /// Token for authorization
    /// </summary>
    private readonly Cached<AuthTokenResult> _authToken;
    private const int AuthTokenExpiresInSec = 23 * 60 * 60;

    /// <summary>
    /// Token for downloading files
    /// </summary>
    private readonly Cached<string> _cachedDownloadToken;
    private const int DownloadTokenExpiresSec = 20 * 60;


    public bool IsAnonymous => _creds.IsAnonymous;
    public string Login => _creds.Login;
    public string Password => _creds.Password;

    public string AccessToken => _authToken.Value?.Token;
    public string DownloadToken => _cachedDownloadToken.Value;

    public void ExpireDownloadToken()
    {
        _cachedDownloadToken.Expire();
    }
}
