using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadPostData
    {
        //1public string Sk { get; set; }
        //1public string IdClient { get; set; }
        public List<YadPostModel> Models { get; set; } = new();

        public byte[] CreateHttpContent()
        {
            var keyValues = new List<KeyValuePair<string, string>>
            {
                //new("sk", Sk),
                //new("idClient", IdClient)
            };

            keyValues.AddRange( Models.SelectMany( ( model, i ) => model.ToKvp( i ) ) );

            FormUrlEncodedContent z = new FormUrlEncodedContent( keyValues );
            return z.ReadAsByteArrayAsync().Result;
        }
    }

    abstract class YadPostModel
    {
        public virtual IEnumerable<KeyValuePair<string, string>> ToKvp(int index)
        {
            yield return new KeyValuePair<string, string>($"_model.{index}", Name);
        }

        public string Name { get; set; }
    }

    abstract class YadRequestModel
    {
        public abstract string Method { get; }
        public abstract string RelationalUri { get; }
    }






    ////public class YadResponceResult
    ////{
    ////    [JsonProperty("uid")]
    ////    public long Uid { get; set; }

    ////    [JsonProperty("login")]
    ////    public string Login { get; set; }

    ////    [JsonProperty("sk")]
    ////    public string Sk { get; set; }

    ////    [JsonProperty("version")]
    ////    public string Version { get; set; }

    ////    [JsonProperty("models")]
    ////    public List<YadResponseModel> Models { get; set; }
    ////}

    public class YadResponseModel
    {
        [JsonProperty( "message" )]
        public string Message { get; set; }

        [JsonProperty( "description" )]
        public string Description { get; set; }

        [JsonProperty( "error" )]
        public string Error { get; set; }

        [JsonProperty( "href" )]
        public string Href { get; set; }

        [JsonProperty( "method" )]
        private string _Method { get; set; }
        public HttpMethod Method
        {
            get
            {
                return _Method switch
                {
                    "GET" => HttpMethod.Get,
                    "PUT" => HttpMethod.Put,
                    "POST" => HttpMethod.Post,
                    "HEAD" => HttpMethod.Head,
                    // Студия ругается, что нет такого
                    //"PATCH" => HttpMethod.Patch,
                    "DELETE" => HttpMethod.Delete,
                    "TRACE" => HttpMethod.Trace,
                    "OPTIONS" => HttpMethod.Options,
                    _ => null
                };
            }
        }

        [JsonProperty( "templated" )]
        public string Tempalted { get; set; }
    }


    public class YadStatusModel : YadResponseModel
    {
        [JsonProperty( "status" )]
        public string Status { get; set; }
    }


    //public class YadResponseModel<TData, TParams> : YadResponseModel
    //    //where TData : YadModelDataBase
    //{
    //    [JsonProperty("params")]
    //    public TParams Params { get; set; }

    //    [JsonProperty("data")]
    //    public TData Data { get; set; }
    //}

    public class YadResponseError
    {
        [JsonProperty("id")]
        public string Id { get; set; }

    }


    public class YadModelDataBase
    {
        [JsonProperty("error")]
        public YadModelDataError Error { get; set; }
    }

    public class YadModelDataError
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("body")]
        public YadModelDataErrorBody Body { get; set; }
    }

    public class YadModelDataErrorBody
    {
        [JsonProperty("code")]
        public long Code { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }
    }
}
