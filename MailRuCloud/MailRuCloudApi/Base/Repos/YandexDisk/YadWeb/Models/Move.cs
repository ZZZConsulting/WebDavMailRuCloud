using System.Text;
using System;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadMoveRequestModel : YadRequestModel
    {
        public YadMoveRequestModel(string sourcePath, string destPath, bool? force = true)
        {
            Source = sourcePath;
            Destination = destPath;
            Force = force;
        }

        public string Source { get; set; }
        public string Destination { get; set; }
        public bool? Force { get; set; }

        public override string Method => "POST";

        public override string RelationalUri
        {
            get
            {
                StringBuilder sb = new StringBuilder( "/v1/disk/resources/move?force_async=true&from=" );
                sb.Append( Uri.EscapeDataString( Source ) );
                sb.Append( "&path=" );
                sb.Append( Uri.EscapeDataString( Destination ) );
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

    internal class YadMoveRequestData : YadModelDataBase
    {
        [JsonProperty("at_version")]
        public long AtVersion { get; set; }

        [JsonProperty("oid")]
        public string Oid { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    internal class YadMoveRequestParams
    {
        [JsonProperty("src")]
        public string Src { get; set; }

        [JsonProperty("dst")]
        public string Dst { get; set; }

        [JsonProperty("force")]
        public long Force { get; set; }
    }
}