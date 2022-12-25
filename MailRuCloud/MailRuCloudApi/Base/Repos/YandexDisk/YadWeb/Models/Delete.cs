using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadDeleteRequestModel : YadRequestModel
    {
        public YadDeleteRequestModel( string path)
        {
            Path = path;
        }

        public string Path { get; set; }

        public override string Method => "DELETE";

        public override string RelationalUri
        {
            get
            {
                StringBuilder sb = new StringBuilder( "/v1/disk/resources?force_async=true&path=" );
                sb.Append( Uri.EscapeDataString( Path ) );

                return sb.ToString();
            }
        }
    }

    public class YadDeleteRequestData : YadModelDataBase
    {
        [JsonProperty("at_version")]
        public long AtVersion { get; set; }

        [JsonProperty("oid")]
        public string Oid { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class YadDeleteRequestParams
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}