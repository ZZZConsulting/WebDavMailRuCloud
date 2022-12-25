using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models
{
    class YadGetResourceRequestModel : YadRequestModel
    {
        public YadGetResourceRequestModel(string path)
        {
            Path = path;
        }

        public string Path { get; set; }

        public override string Method => "GET";

        public override string RelationalUri
        {
            get
            {
                StringBuilder sb = new StringBuilder( "/v1/disk/resources/download?path=" );
                sb.Append( Uri.EscapeDataString( Path ) );

                return sb.ToString();
            }
        }
    }
}