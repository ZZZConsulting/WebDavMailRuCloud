using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommandLine;
using YaR.Clouds.Base;

namespace YaR.Clouds.Console
{
    // ReSharper disable once ClassNeverInstantiated.Global
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    internal class CommandLineOptions
    {
        [Option('p', "port", Separator = ',', Required = false, Default = new[] { 801 }, HelpText = "WebDAV server port")]
        public IEnumerable<int> Port { get; set; }

        [Option('h', "host", Required = false, Default = "http://127.0.0.1", HelpText = "WebDAV server host, including protocol")]
        public string Host { get; set; }

        [Option("proxy-address", Default = "", HelpText = "Proxy address i.e. http://192.168.1.1:8080")]
        public string ProxyAddress { get; set; }

        [Option("proxy-user", Default = "", HelpText = "Proxy user")]
        public string ProxyUser { get; set; }

        [Option("proxy-password", Default = "", HelpText = "Proxy password")]
        public string ProxyPassword { get; set; }

        [Option("maxthreads", Default = 5, HelpText = "Maximum concurrent listening connections to the service")]
        public int MaxThreadCount { get; set; }

        [Option("maxconnections", Default = 10, HelpText = "Maximum concurrent connections to cloud server per instance")]
        public int MaxConnectionCount { get; set; }

        [Option("use-locks", Required = false, Default = false, HelpText = "locking feature")]
        public bool UseLocks { get; set; }

        [Option("cache-listing", Default = 30, HelpText = "Timeout of in-memory cache of cloud names of files and folders, sec")]
        public int CacheListingSec { get; set; }

        [Option("cache-listing-depth", Default = 1, HelpText = "Depth of folders listings, always equals 1 when cache-listing>0")]
        public int CacheListingDepth { get; set; }

        [Option("use-deduplicate", Required = false, Default = false,
            HelpText = "Use cloud deduplicate feature to minimize traffic")]
        public bool UseDeduplicate { get; set; }

        [Option("disable-links", Required = false, Default = false,
            HelpText = "Disable support for shared folder and links stored in item.links.wdmrc files")]
        public bool DisableLinkManager { get; set; }

        [Option("protocol", Default = Protocol.Autodetect, HelpText = "Cloud protocol")]
        public Protocol Protocol { get; set; }

        [Option("install", Required = false, HelpText = "Install as Windows service")]
        public string ServiceInstall { get; set; }

        [Option("install-display", Required = false, HelpText = "'Display name' of the service when installed as Windows service")]
        public string ServiceInstallDisplayName { get; set; }

        [Option("uninstall", Required = false, HelpText = "Uninstall Windows service")]
        public string ServiceUninstall { get; set; }

        [Option("100-continue-timeout-sec", Required = false, Default = 1,
            HelpText = "Timeout in seconds, to wait until the 100-Continue is received")]
        public int Wait100ContinueTimeoutSec { get; set; }

        [Option("response-timeout-sec", Required = false, Default = 100,
            HelpText = "Timeout in seconds, to wait until 1-st byte from server is received")]
        public int WaitResponseTimeoutSec { get; set; }

        [Option("read-write-timeout-sec", Required = false, Default = 300,
            HelpText = "Timeout in seconds, the maximum duration of read or write operation")]
        public int ReadWriteTimeoutSec { get; set; }

        [Option("cloud-instance-timeout", Required = false, Default = 30,
            HelpText = "Cloud instance (server+login) expiration timeout in minutes")]
        public int CloudInstanceTimeoutMinutes { get; set; }

        [Option("user-agent", HelpText = "Overrides default 'user-agent' header in requests to cloud servers")]
        public string UserAgent { get; set; }

        [Option("sec-ch-ua", HelpText = "Overrides default 'sec-ch-ua' header in requests to cloud servers.")]
        public string SecChUa { get; set; }

        [Option("detect-activity-interval", Required = false, Default = 15,
            HelpText = "Interval in seconds to track changes on Yandex.Disk made behind the Emulator, 0 - disable tracking")]
        public int DetectActivityInterval { get; set; }

        [Option("service", Required = false, Default = false, HelpText = "Started as a service")]
        public bool ServiceRun { get; set; }
    }
}
