using System;
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
    class YadCommonRequest : BaseRequestJson<YadResponseModel>
    {
        //private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YaDCommonRequest));

        private string _uri;
        private string _method;

        private readonly List<object> _outData = new(1);

        private YadWebAuth YadAuth { get; }

        public YadCommonRequest(HttpCommonSettings settings, YadWebAuth auth) : base(settings, auth)
        {
            YadAuth = auth;
        }

        public YadCommonRequest With<T, TOut>(T model, out TOut resOUt)
            where T : YadRequestModel 
            where TOut : YadResponseModel, new()
        {
            _uri = model.RelationalUri;
            _method = model.Method;
            _outData.Add(resOUt = new TOut());

            return this;
        }

        protected override string Method => _method;
        protected override string RelationalUri => _uri;

        protected override HttpWebRequest CreateRequest( string baseDomain = null )
        {
            var request = base.CreateRequest( ConstSettings.YandexCloudDomain );
            request.Headers[ "Authorization" ] = $"OAuth {Auth.AccessToken}";

            return request;
        }

        protected override RequestResponse<YadResponseModel> DeserializeMessage(NameValueCollection responseHeaders, System.IO.Stream stream)
        {
            using var sr = new StreamReader(stream);

            string text = sr.ReadToEnd();
            //Logger.Debug(text);

            var msg = new RequestResponse<YadResponseModel>
            {
                Ok = true,
                //1Result = JsonConvert.DeserializeObject<YadResponseModel>(text, new KnownYadModelConverter(_outData))
            };

            if( _outData.Count > 0 )
            {
                JsonConvert.PopulateObject( text, _outData[ 0 ] );
                //_outData[ 0 ] = JsonConvert.DeserializeObject( text, _outData[ 0 ].GetType() );
                msg.Result = _outData[ 0 ] as YadResponseModel;
            }

            return msg;
        }
    }
}