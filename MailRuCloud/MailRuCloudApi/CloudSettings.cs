﻿using System.Net;
using YaR.Clouds.Base;
using YaR.Clouds.Base.Streams.Cache;
using YaR.Clouds.Common;

namespace YaR.Clouds;

public class CloudSettings
{
    public ITwoFaHandler TwoFaHandler { get; set; }

    public string UserAgent { get; set; }
    public string SecChUa { get; set; }

    public Protocol Protocol { get; set; } = Protocol.Autodetect;

    public int CacheListingSec { get; set; } = 30;

    public int MaxConnectionCount { get; set; } = 10;

    public int ListDepth
    {
        get => CacheListingSec > 0 ? _listDepth : 1;
        set => _listDepth = value;
    }
    private int _listDepth = 1;

    public string SpecialCommandPrefix { get; set; } = ">>";
    public string AdditionalSpecialCommandPrefix { get; set; } = ">>";

    public SharedVideoResolution DefaultSharedVideoResolution { get; set; } = SharedVideoResolution.All;
    public IWebProxy Proxy { get; set; }
    public bool UseLocks { get; set; }

    public bool UseDeduplicate { get; set; }

    public DeduplicateRulesBag DeduplicateRules { get; set; }

    public bool DisableLinkManager { get; set; }

    public int CloudInstanceTimeoutMinutes { get; set; }

    #region Connection timeouts

    public int Wait100ContinueTimeoutSec { get; set; }
    public int WaitResponseTimeoutSec { get; set; }
    public int ReadWriteTimeoutSec { get; set; }

    #endregion
    #region BrowserAuthenticator

    public string BrowserAuthenticatorUrl { get; set; }

    public string BrowserAuthenticatorPassword { get; set; }

    public string BrowserAuthenticatorCacheDir { get; set; }

    #endregion
}
