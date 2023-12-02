﻿using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests
{
    //internal class WeblinkGetServerRequest : ServerRequest
    //{
    //    public WeblinkGetServerRequest(HttpCommonSettings settings) : base(settings)
    //    {
    //    }

    //    protected override string RelationalUri => "https://dispatcher.cloud.mail.ru/G";
    //}

    class WeblinkGetServerRequest : BaseRequestJson<ShardInfoRequestResult>
    {
        public WeblinkGetServerRequest(HttpCommonSettings settings)
            : base(settings, null)
        {
        }

        protected override string RelationalUri => $"{_settings.BaseDomain}/api/v2/dispatcher?api=2&email=anonym&x-email=anonym";
    }
}
