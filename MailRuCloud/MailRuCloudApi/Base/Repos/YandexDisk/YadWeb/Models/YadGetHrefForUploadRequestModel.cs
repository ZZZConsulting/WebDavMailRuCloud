using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadGetHrefForUploadRequestModel : YadRequestModel
    {
        public YadGetHrefForUploadRequestModel( string path, bool? force = true)
        {
            Path = path;
            Force = force;
        }

        public string Path { get; set; }
        public bool? Force { get; set; }

        public override string Method => "GET";

        public override string RelationalUri
        {
            get
            {
                StringBuilder sb = new StringBuilder( "/v1/disk/resources/upload?path=" );
                sb.Append( Uri.EscapeDataString( Path ) );
                if( Force.HasValue )
                {
                    sb.Append( "&overwrite=" );
                    if( Force == true )
                        sb.Append( "true" );
                    if( Force == false )
                        sb.Append( "false" );
                }

                return sb.ToString();
            }
        }
    }

    internal class YadGetResourceUploadResponse : YadResponseModel
    {
        // upload params
        [JsonProperty("operation_id")]
        public string OperationId { get; set; }
    }
}
