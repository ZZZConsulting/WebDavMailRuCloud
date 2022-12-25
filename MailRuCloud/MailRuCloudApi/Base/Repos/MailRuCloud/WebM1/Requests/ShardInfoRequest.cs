﻿using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests
{
    class ShardInfoRequest : BaseRequestJson<ShardInfoRequestResult>
    {
        public ShardInfoRequest(HttpCommonSettings settings, IAuth auth) : base(settings, auth)
        {
        }

        protected override string RelationalUri
        {
            get
            {
                var uri = $"{ConstSettings.MailCloudDomain}/api/m1/dispatcher?client_id={Settings.ClientId}";
                if (!string.IsNullOrEmpty(Auth.AccessToken))
                    uri += $"&access_token={Auth.AccessToken}";
                return uri;
            }
        }
    }
}
