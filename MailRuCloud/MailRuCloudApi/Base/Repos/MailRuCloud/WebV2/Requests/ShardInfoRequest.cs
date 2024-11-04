using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebV2.Requests;

class ShardInfoRequest : BaseRequestJson<ShardInfoRequestResult>
{
    public ShardInfoRequest(HttpCommonSettings settings, IAuth auth)
        : base(settings, auth)
    {
    }

    protected override string RelationalUri
    {
        get
        {
            var uri = $"{_settings.BaseDomain}/api/v2/dispatcher?client_id={_settings.ClientId}";
            if (!_auth.IsAnonymous)
                uri += $"&access_token={_auth.AccessToken}";
            else
            {
                uri += "&email=anonym&x-email=anonym";
            }
            return uri;
        }
    }
}
