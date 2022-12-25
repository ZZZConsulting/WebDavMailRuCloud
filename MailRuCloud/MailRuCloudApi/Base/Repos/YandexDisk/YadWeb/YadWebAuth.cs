using System;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb
{
    class YadWebAuth : IAuth
    {
        const string TOKEN_PREFIX = "token:";
        public YadWebAuth(HttpCommonSettings settings, IBasicCredentials creds)
        {
            _settings = settings;
            if( creds.Password != null && creds.Password.StartsWith( TOKEN_PREFIX, StringComparison.OrdinalIgnoreCase ) )
            {
                _creds = new Credentials( creds.Login, "use token!" );
                AccessToken = creds.Password.Remove( 0, TOKEN_PREFIX.Length ).Trim();
            }
            else
            {
                _creds = creds;
            }
            Cookies = new CookieContainer();

            var _ = MakeLogin().Result;
        }

        private readonly IBasicCredentials _creds;
        private readonly HttpCommonSettings _settings;

        public async Task<bool> MakeLogin()
        {
            //1var preAuthResult = await new YadPreAuthRequest(_settings, this)
            //1    .MakeRequestAsync();
            //1if (string.IsNullOrWhiteSpace(preAuthResult.Csrf))
            //1    throw new AuthenticationException($"{nameof(YadPreAuthRequest)} error parsing csrf");
            //1if (string.IsNullOrWhiteSpace(preAuthResult.ProcessUUID))
            //1    throw new AuthenticationException($"{nameof(YadPreAuthRequest)} error parsing ProcessUUID");

            //1Uuid = preAuthResult.ProcessUUID;

            //var loginAuth = await new YadAuthLoginRequest(_settings, this, preAuthResult.Csrf, preAuthResult.ProcessUUID)
            //        .MakeRequestAsync();
            //if (loginAuth.HasError)
            //    throw new AuthenticationException($"{nameof(YadAuthLoginRequest)} error");

            //1var passwdAuth = await new YadAuthPasswordRequest(_settings, this, preAuthResult.Csrf, loginAuth.TrackId)
            //1    .MakeRequestAsync();
            //1if (passwdAuth.HasError)
            //1    throw new AuthenticationException($"{nameof(YadAuthPasswordRequest)} errors: {passwdAuth.Errors.Aggregate((f,s) => f + "," + s)}");


            var diskInfo = await new YadDiskInfoRequest( _settings, this )
                .MakeRequestAsync();
            if ( diskInfo.HasError)
                throw new AuthenticationException($"{nameof(YadDiskInfoRequest)} error");

            //1var askv2 = await new YadAuthAskV2Request(_settings, this,  accsAuth.Csrf, passwdAuth.DefaultUid)
            //1    .MakeRequestAsync();
            //1if (accsAuth.HasError)
            //1    throw new AuthenticationException($"{nameof(YadAuthAskV2Request)} error");

            //1var skReq = await new YadAuthDiskSkRequest(_settings, this)
            //1    .MakeRequestAsync();
            //1if (skReq.HasError)
            //1    throw new AuthenticationException($"{nameof(YadAuthDiskSkRequest)} error, response: {skReq.HtmlResponse}");
            //1DiskSk = skReq.DiskSk;

            //Csrf = preAuthResult.Csrf;

            return true;
        }

        public string Login => _creds.Login;
        //1public string Password => _creds.Password;
        public string Password => null;
        //1public string DiskSk { get; set; }
        //1public string Uuid { get; set; }
        //public string Csrf { get; set; }



        public bool IsAnonymous => false;
        public string AccessToken { get; private set; }
        public string DownloadToken { get; }
        public CookieContainer Cookies { get; }
        public void ExpireDownloadToken()
        {
            throw new NotImplementedException();
        }
    }
}
