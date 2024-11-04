﻿using System;
using System.Text;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests;

class MoveRequest : BaseRequestJson<CommonOperationResult<string>>
{
    private readonly string _sourceFullPath;
    private readonly string _destinationPath;

    public MoveRequest(HttpCommonSettings settings, IAuth auth, string sourceFullPath, string destinationPath)
        : base(settings, auth)
    {
        _sourceFullPath = sourceFullPath;
        _destinationPath = destinationPath;
    }

    protected override string RelationalUri => $"/api/m1/file/move?access_token={_auth.AccessToken}";

    protected override byte[] CreateHttpContent()
    {
        var data = $"home={Uri.EscapeDataString(_sourceFullPath)}&email={_auth.Login}&conflict=rename&folder={Uri.EscapeDataString(_destinationPath)}";
        return Encoding.UTF8.GetBytes(data);
    }
}
