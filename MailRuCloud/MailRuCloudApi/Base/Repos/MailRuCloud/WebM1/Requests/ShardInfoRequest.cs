using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests;

class ShardInfoRequest : BaseRequestJson<ShardInfoRequestResult>
{
    public ShardInfoRequest(HttpCommonSettings settings, IAuth auth) : base(settings, auth)
    {
    }

    protected override string RelationalUri
    {
        get
        {
            var uri = $"{_settings.BaseDomain}/api/m1/dispatcher?client_id={_settings.ClientId}";
            if (!string.IsNullOrEmpty(_auth.AccessToken))
                uri += $"&access_token={_auth.AccessToken}";
            return uri;
        }
    }
}
