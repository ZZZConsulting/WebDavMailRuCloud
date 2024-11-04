using System;
using System.Text;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests;

class PublishRequest : BaseRequestJson<CommonOperationResult<string>>
{
    private readonly string _fullPath;

    public PublishRequest(HttpCommonSettings settings, IAuth auth, string fullPath)
        : base(settings, auth)
    {
        _fullPath = fullPath;
    }

    protected override string RelationalUri => $"/api/m1/file/publish?access_token={_auth.AccessToken}";

    protected override byte[] CreateHttpContent()
    {
        var data = $"home={Uri.EscapeDataString(_fullPath)}&email={_auth.Login}&x-email={_auth.Login}";
        return Encoding.UTF8.GetBytes(data);
    }
}
