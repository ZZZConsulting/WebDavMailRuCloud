﻿namespace YaR.Clouds.Base.Requests.Types;

/// <summary>
/// Get cloud disk usage for current account.
/// </summary>
public class DiskUsage
{
    /// <summary>
    /// Gets total disk size.
    /// </summary>
    public FileSize Total { get; set; }

    /// <summary>
    /// Gets used disk size.
    /// </summary>
    public FileSize Used { get; set; }

    /// <summary>
    /// Gets free disk size.
    /// </summary>
    public FileSize Free => Total - Used;

    public bool OverQuota { get; set; }
}
