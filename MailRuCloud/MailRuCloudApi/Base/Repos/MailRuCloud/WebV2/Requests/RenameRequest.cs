﻿using System;
using System.Text;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebV2.Requests;

class RenameRequest : BaseRequestJson<CommonOperationResult<string>>
{
    private readonly string _fullPath;
    private readonly string _newName;

    public RenameRequest(HttpCommonSettings settings, IAuth auth, string fullPath, string newName) : base(settings, auth)
    {
        _fullPath = fullPath;
        _newName = newName;
    }

    protected override string RelationalUri => "/api/v2/file/rename";

    protected override byte[] CreateHttpContent()
    {
        var data = string.Format("home={0}&api={1}&token={2}&email={3}&x-email={3}&conflict=rename&name={4}", Uri.EscapeDataString(_fullPath),
            2, _auth.AccessToken, _auth.Login, Uri.EscapeDataString(_newName));
        return Encoding.UTF8.GetBytes(data);
    }
}
