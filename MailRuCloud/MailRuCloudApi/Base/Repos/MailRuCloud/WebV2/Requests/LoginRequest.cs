﻿using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebV2.Requests
{
    class LoginRequest : BaseRequestString<LoginResult>
    {
        public LoginRequest(HttpCommonSettings settings, IAuth auth) 
            : base(settings, auth)
        {
        }

        protected override HttpWebRequest CreateRequest(string baseDomain = null)
        {
            var request = base.CreateRequest(CommonSettings.AuthDomain);
            request.Accept = CommonSettings.DefaultAcceptType;
            return request;
        }

        protected override string RelationalUri => "/cgi-bin/auth";

        protected override byte[] CreateHttpContent()
        {
#pragma warning disable SYSLIB0013 // Type or member is obsolete
            string data = $"Login={Uri.EscapeUriString(Auth.Login)}&Domain={CommonSettings.Domain}&Password={Uri.EscapeUriString(Auth.Password)}";
#pragma warning restore SYSLIB0013 // Type or member is obsolete

            return Encoding.UTF8.GetBytes(data);
        }

        protected override RequestResponse<LoginResult> DeserializeMessage(NameValueCollection responseHeaders, string responseText)
        {
            var csrf = responseText.Contains("csrf")
                ? new string(responseText.Split(new[] {"csrf"}, StringSplitOptions.None)[1].Split(',')[0].Where(char.IsLetterOrDigit).ToArray())
                : string.Empty;

            var msg = new RequestResponse<LoginResult>
            {
                Ok = true,
                Result = new LoginResult
                {
                    Csrf = csrf
                }
            };
            return msg;
        }
    }
}
