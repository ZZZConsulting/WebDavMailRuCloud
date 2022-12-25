using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadFolderRequestModel : YadRequestModel
    {
        private readonly string _pathPrefix;

        public YadFolderRequestModel(string path, string pathPrefix = "")
        {
            _pathPrefix = pathPrefix;
            Path = path;
        }

        public string Path { get; set; }
        //1public int Order { get; set; } = 1;
        public string SortBy { get; set; } = "name";
        public int Offset { get; set; } = 0;
        public int Amount { get; set; } = int.MaxValue;

        public override string Method => "GET";

        public override string RelationalUri
        {
            get
            {
                StringBuilder sb = new StringBuilder( "/v1/disk/resources?path=" );
                sb.Append( Uri.EscapeDataString( Path ) );
                if( Amount != int.MaxValue )
                {
                    sb.Append( "&limit=" );
                    sb.Append( Amount );
                }
                if( Offset != 0 )
                {
                    sb.Append( "&offset=" );
                    sb.Append( Offset );
                }
                sb.Append( "&sort=" );
                sb.Append( Uri.EscapeDataString( SortBy ) );

                return sb.ToString();
            }
        }
    }

    internal class YadDirEntryInfo : YadResponseModel
    {
        [JsonProperty( "type" )]
        public string Type { get; set; }

        [JsonProperty( "_embedded" )]
        public YadFolderResponseEmbedded Content { get; set; }

        [JsonProperty( "name" )]
        public string Name { get; set; }

        [JsonProperty( "size" )]
        public long? Size { get; set; }

        [JsonProperty( "created" )]
        public DateTime? Created { get; set; }

        [JsonProperty( "modified" )]
        public DateTime? Modified { get; set; }

        [JsonProperty( "path" )]
        public string Path { get; set; }

        [JsonProperty( "file" )]
        public string DownloadUrl { get; set; }

        [JsonProperty( "public_key" )]
        public string PublickKey { get; set; }

        [JsonProperty( "public_url" )]
        public string PublickUrl { get; set; }
    }

    internal class YadFolderResponseEmbedded
    {
        [JsonProperty( "sort" )]
        public string Sort { get; set; }

        [JsonProperty( "limit" )]
        public int Amount { get; set; }

        [JsonProperty( "offset" )]
        public int Offset { get; set; }

        [JsonProperty( "path" )]
        public string Path { get; set; }

        [JsonProperty( "total" )]
        public int Count { get; set; }

        [JsonProperty( "items" )]
        public List<YadDirEntryInfo> Items { get; set; }
    }
    

    class Size
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    class VideoInfo
    {
        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("creationTime")]
        public long CreationTime { get; set; }

        [JsonProperty("streams")]
        public List<Stream> Streams { get; set; }

        [JsonProperty("startTime")]
        public long StartTime { get; set; }

        [JsonProperty("duration")]
        public long Duration { get; set; }

        [JsonProperty("bitRate")]
        public long BitRate { get; set; }
    }

    class Stream
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("frameRate", NullValueHandling = NullValueHandling.Ignore)]
        public long? FrameRate { get; set; }

        [JsonProperty("displayAspectRatio", NullValueHandling = NullValueHandling.Ignore)]
        public DisplayAspectRatio DisplayAspectRatio { get; set; }

        [JsonProperty("codec")]
        public string Codec { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("bitRate")]
        public long BitRate { get; set; }

        [JsonProperty("dimension", NullValueHandling = NullValueHandling.Ignore)]
        public Dimension Dimension { get; set; }

        [JsonProperty("channelsCount", NullValueHandling = NullValueHandling.Ignore)]
        public long? ChannelsCount { get; set; }

        [JsonProperty("stereo", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Stereo { get; set; }

        [JsonProperty("sampleFrequency", NullValueHandling = NullValueHandling.Ignore)]
        public long? SampleFrequency { get; set; }
    }

    class Dimension
    {
        [JsonProperty("width")]
        public long Width { get; set; }

        [JsonProperty("height")]
        public long Height { get; set; }
    }

    class DisplayAspectRatio
    {
        [JsonProperty("denom")]
        public long Denom { get; set; }

        [JsonProperty("num")]
        public long Num { get; set; }
    }

    internal class YadFolderInfoRequestParams
    {
        [JsonProperty("idContext")]
        public string IdContext { get; set; }

        [JsonProperty("order")]
        public long Order { get; set; }

        [JsonProperty("sort")]
        public string Sort { get; set; }

        [JsonProperty("offset")]
        public long Offset { get; set; }

        [JsonProperty("amount")]
        public long Amount { get; set; }
    }
}