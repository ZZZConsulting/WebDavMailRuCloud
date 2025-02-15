﻿using System;
using System.Text;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests;

class OAuthSecondStepRequest : BaseRequestJson<OAuthRequest.Result>
{
    private readonly string _login;
    private readonly string _tsaToken;
    private readonly string _authCode;

    public OAuthSecondStepRequest(HttpCommonSettings settings, string login, string tsaToken, string authCode)
        : base(settings, null)
    {
        _login = login;
        _tsaToken = tsaToken;
        _authCode = authCode;
    }

    protected override string RelationalUri => "https://o2.mail.ru/token";

    protected override byte[] CreateHttpContent()
    {
#pragma warning disable SYSLIB0013 // Type or member is obsolete
        var data = $"client_id={_settings.ClientId}&grant_type=password&username={Uri.EscapeUriString(_login)}&tsa_token={_tsaToken}&auth_code={_authCode}";
#pragma warning restore SYSLIB0013 // Type or member is obsolete
        return Encoding.UTF8.GetBytes(data);
    }
}
