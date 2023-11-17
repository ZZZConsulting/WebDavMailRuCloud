﻿using System;
using System.Text;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests
{
    class RenameRequest : BaseRequestJson<CommonOperationResult<string>>
    {
        private readonly string _fullPath;
        private readonly string _newName;

        public RenameRequest(HttpCommonSettings settings, IAuth auth, string fullPath, string newName)
            : base(settings, auth)
        {
            _fullPath = fullPath;
            _newName = newName;
        }

        protected override string RelationalUri => $"/api/m1/file/rename?access_token={_auth.AccessToken}";

        protected override byte[] CreateHttpContent()
        {
            var data = $"home={Uri.EscapeDataString(_fullPath)}&email={_auth.Login}&conflict=rename&name={Uri.EscapeDataString(_newName)}";
            return Encoding.UTF8.GetBytes(data);
        }
    }
}
