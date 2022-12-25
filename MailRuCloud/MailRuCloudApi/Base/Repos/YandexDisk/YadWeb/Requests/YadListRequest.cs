using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests
{
    class YadListRequest : BaseRequestJson<YadItemInfoRequestData>
    {

        private readonly YadPostData _postData = new();

        private readonly List<object> _outData = new();

        public YadListRequest( HttpCommonSettings settings, YadWebAuth auth)
            : base(settings, auth)
        {
        }

        public YadListRequest With<T, TOut>(T model, out TOut resOUt)
            where T : YadPostModel 
            where TOut : YadResponseModel, new()
        {
            _postData.Models.Add(model);
            _outData.Add(resOUt = new TOut());

            return this;
        }

        protected override string RelationalUri => "/v1/disk/resources";

        protected override HttpWebRequest CreateRequest( string baseDomain = null )
        {
            var request = base.CreateRequest( ConstSettings.YandexCloudDomain );
            request.Headers[ "Authorization" ] = $"OAuth {Auth.AccessToken}";

            return request;
        }

        //protected override RequestResponse<YadResponceResult> DeserializeMessage(NameValueCollection responseHeaders, System.IO.Stream stream)
        //{
        //    using var sr = new StreamReader(stream);

        //    string text = sr.ReadToEnd();
        //    //Logger.Debug(text);

        //    var msg = new RequestResponse<YadResponceResult>
        //    {
        //        Ok = true,
        //        Result = JsonConvert.DeserializeObject<YadResponceResult>(text, new KnownYadModelConverter(_outData))
        //    };
        //    return msg;
        //}

    }
}