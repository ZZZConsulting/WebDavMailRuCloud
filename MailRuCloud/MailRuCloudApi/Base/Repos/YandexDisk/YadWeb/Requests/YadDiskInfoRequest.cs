using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests
{
    class YadDiskInfoRequest : BaseRequestJson<YadInfoRequestResult>
    {
        public YadDiskInfoRequest( HttpCommonSettings settings, IAuth auth )
            : base(settings, auth)
        {
        }

        protected override string RelationalUri => "/v1/disk";

        protected override HttpWebRequest CreateRequest( string baseDomain = null )
        {
            var request = base.CreateRequest( ConstSettings.YandexCloudDomain );
            request.Headers[ "Authorization" ] = $"OAuth {Auth.AccessToken}";

            return request;
        }
        //protected override byte[] CreateHttpContent()
        //{
        //    var keyValues = new List<KeyValuePair<string, string>>
        //    {
        //        //new("login", _auth.Login),
        //    };
        //    FormUrlEncodedContent z = new FormUrlEncodedContent(keyValues);
        //    var d = z.ReadAsByteArrayAsync().Result;
        //    return d;
        //}
    }



    class YadInfoRequestUserResult
    {
        [JsonProperty( "login" )]
        public string Login { get; set; }

        [JsonProperty( "display_name" )]
        public string DisplayName { get; set; }

        [JsonProperty( "uid" )]
        public string UID { get; set; }
    
        [JsonProperty( "country" )]
        public string Locale { get; set; }
    }
    class YadInfoRequestResult
    {
        public bool HasError => User?.Login == null;

        [JsonProperty( "user" )]
        public YadInfoRequestUserResult User { get; set; }

        [JsonProperty("is_paid")]
        public bool IsPaid { get; set; }

        [JsonProperty("max_file_size")]
        public long MaxFileSize { get; set; }

        [JsonProperty("paid_max_file_size")]
        public long PaidMaxFileSize { get; set; }

        [JsonProperty("total_space")]
        public long TotalSpace { get; set; }

        [JsonProperty("used_space")]
        public long UsedSpace { get; set; }

        [JsonProperty("errors")]
        public List<string> Errors { get; set; }
    }

}