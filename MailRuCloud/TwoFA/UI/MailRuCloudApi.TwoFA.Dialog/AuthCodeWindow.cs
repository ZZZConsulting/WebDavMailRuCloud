﻿using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace YaR.Clouds.MailRuCloud.TwoFA.UI
{
    // ReSharper disable once UnusedType.Global
    public class AuthCodeWindow : ITwoFaHandler
    {
        public AuthCodeWindow(IEnumerable<KeyValuePair<string, string>> parameters)
        {
        }

        public string Get(string login, bool isAutoRelogin)
        {
            Application.EnableVisualStyles();

            using (var notify = new NotifyIcon())
            using (var prompt = new AuthCodeForm())
            {
                var prompt1 = prompt;

                notify.Visible = true;
                notify.Icon = SystemIcons.Exclamation;
                notify.BalloonTipTitle = "WebDAV Mail.Ru 2 Factor Auth";
                notify.BalloonTipText = $"Auth code required for {login}";
                notify.BalloonTipIcon = ToolTipIcon.Info;

                notify.Click += (sender, args) =>
                    prompt1.Activate();
                notify.BalloonTipClicked += (sender, args) =>
                    prompt1.Activate();

                notify.ShowBalloonTip(30000);

                var res = prompt.ShowAuthDialog(login, isAutoRelogin);

                return res.AuthCode;
            }
        }
    }
}
