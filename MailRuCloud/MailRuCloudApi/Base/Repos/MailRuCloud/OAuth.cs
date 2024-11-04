using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Common;
using static YaR.Clouds.Cloud;

namespace YaR.Clouds.Base.Repos.MailRuCloud;

internal class OAuth : IAuth
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(OAuth));

    private readonly SemaphoreSlim _connectionLimiter;
    private readonly HttpCommonSettings _settings;
    private readonly IBasicCredentials _creds;

    private readonly AuthCodeRequiredDelegate _onAuthCodeRequired;

    public OAuth(SemaphoreSlim connectionLimiter,
        HttpCommonSettings settings, IBasicCredentials credentials, AuthCodeRequiredDelegate onAuthCodeRequired)
    {
        _settings = settings;
        _connectionLimiter = connectionLimiter;
        _creds = credentials;
        _onAuthCodeRequired = onAuthCodeRequired;
        Cookies = new CookieContainer();

        _authToken = new Cached<AuthTokenResult>(old =>
            {
                Logger.Debug(old is null
                    ? "OAuth: authorizing."
                    : "OAuth: AuthToken expired, refreshing.");

                var token = string.IsNullOrEmpty(old?.RefreshToken)
                    ? Auth().Result
                    : Refresh(old.RefreshToken).Result;

                return token;
            },
            value => value?.ExpiresIn.Add(-TimeSpan.FromMinutes(5)) ?? TimeSpan.MaxValue);
        //value => TimeSpan.FromSeconds(20));
    }

    public bool IsAnonymous => _creds.IsAnonymous;
    public string Login => _creds.Login;
    public string Password => _creds.Password;
    public string AccessToken => _authToken.Value?.Token;
    public string DownloadToken => _authToken.Value?.Token;
    public CookieContainer Cookies { get; }

    public void ExpireDownloadToken()
    {
    }

    /// <summary>
    /// Token for authorization in mobile version
    /// </summary>
    private readonly Cached<AuthTokenResult> _authToken;

    private async Task<AuthTokenResult> Auth()
    {
        if (_creds.IsAnonymous)
            return null;

        var req = await new OAuthRequest(_settings, _creds).MakeRequestAsync(_connectionLimiter);
        var res = req.ToAuthTokenResult();

        if (!res.IsSecondStepRequired)
            return res;

        if (null == _onAuthCodeRequired)
            throw new Exception("No 2Factor plugin found.");

        string code = _onAuthCodeRequired.Invoke(_creds.Login, true);
        if (string.IsNullOrWhiteSpace(code))
            throw new Exception("Empty 2Factor code");

        var ssreq = await new OAuthSecondStepRequest(_settings, _creds.Login, res.TsaToken, code)
            .MakeRequestAsync(_connectionLimiter);

        res = ssreq.ToAuthTokenResult();

        return res;
    }

    private async Task<AuthTokenResult> Refresh(string refreshToken)
    {
        var req = await new OAuthRefreshRequest(_settings, refreshToken).MakeRequestAsync(_connectionLimiter);
        var res = req.ToAuthTokenResult(refreshToken);
        return res;
    }
}
