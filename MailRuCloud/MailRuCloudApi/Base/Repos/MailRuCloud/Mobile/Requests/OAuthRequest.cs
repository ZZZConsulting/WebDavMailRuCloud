﻿using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests
{
    class OAuthRequest: BaseRequestJson<OAuthRequest.Result>
    {
        private readonly string _login;
        private readonly string _password;

        public OAuthRequest(HttpCommonSettings settings, IBasicCredentials credentials) : base(settings, null)
        {
            _login = credentials.Login;
            _password = credentials.Password;
        }

        protected override string RelationalUri => "https://o2.mail.ru/token";

        protected override byte[] CreateHttpContent()
        {
            var keyValues = new List<KeyValuePair<string, string>>
            {
                new("username", _login),
                new("password", _password),
                new("client_id", Settings.ClientId),
                new("grant_type", "password")
            };
            return new FormUrlEncodedContent(keyValues).ReadAsByteArrayAsync().Result;
        }

        public class Result
        {
            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }
            [JsonProperty("refresh_token")]
            public string RefreshToken { get; set; }
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }

            [JsonProperty("error")]
            public string Error { get; set; }
            [JsonProperty("error_code")]
            public int ErrorCode { get; set; }
            [JsonProperty("error_description")]
            public string ErrorDescription { get; set; }

            /// <summary>
            /// Token for second step auth
            /// </summary>
            [JsonProperty("tsa_token")]
            public string TsaToken { get; set; }
            /// <summary>
            /// Code length for second step auth
            /// </summary>
            [JsonProperty("length")]
            public int Length { get; set; }
            /// <summary>
            /// Seconds to wait for for second step auth code
            /// </summary>
            [JsonProperty("timeout")]
            public int Timeout { get; set; }
        }
    }
}
