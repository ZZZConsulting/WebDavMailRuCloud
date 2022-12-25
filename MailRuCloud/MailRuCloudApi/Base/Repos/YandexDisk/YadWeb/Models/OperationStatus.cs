using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    internal class YadOperationStatusRequestModel : YadRequestModel
    {
        public YadOperationStatusRequestModel( string oid)
        {
            Oid = oid;
        }

        public string Oid { get; set; }


        public override string Method => "GET";

        public override string RelationalUri
        {
            get
            {
                StringBuilder sb = new StringBuilder( "/v1/disk/operations/" );
                sb.Append( Uri.EscapeDataString( Oid ) );

                return sb.ToString();
            }
        }

    }

    internal class YadOperationStatusData : YadModelDataBase
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("at_version")]
        public long AtVersion { get; set; }
    }

    internal class YadOperationStatusParams
    {
        [JsonProperty("oid")]
        public string Oid { get; set; }
    }
}
