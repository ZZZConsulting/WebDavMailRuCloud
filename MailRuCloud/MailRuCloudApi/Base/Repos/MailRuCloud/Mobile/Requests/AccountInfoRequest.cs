using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests;

class AccountInfoRequest : BaseRequestJson<AccountInfoRequestResult>
{
    public AccountInfoRequest(HttpCommonSettings settings, IAuth auth) : base(settings, auth)
    {
    }

    protected override string RelationalUri => $"{_settings.BaseDomain}/api/m1/user?access_token={_auth.AccessToken}";
}
