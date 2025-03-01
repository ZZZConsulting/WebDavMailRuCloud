using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NWebDav.Server;
using NWebDav.Server.Http;
using NWebDav.Server.HttpListener;
using NWebDav.Server.Locking;
using NWebDav.Server.Logging;
using YaR.Clouds.Extensions;
using YaR.Clouds.WebDavStore;
using YaR.Clouds.WebDavStore.StoreBase;
using RequestHandlerFactory = YaR.Clouds.WebDavStore.RequestHandlerFactory;

namespace YaR.Clouds.Console
{
    internal static class Payload
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Program));

        public static readonly CancellationTokenSource CancelToken = new();

        public static void Stop()
        {
            CancelToken.Cancel(false);
        }

        public static void Run(CommandLineOptions options)
        {
            // trying to fix "infinite recursion during resource lookup with system.private.corelib"
            // .Net Core 2.0.0
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-us");
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-us");

            log4net.GlobalContext.Properties["protocol"] = options.Protocol;
            log4net.GlobalContext.Properties["port"] = options.Port;

            var repo = log4net.LogManager.CreateRepository(Assembly.GetEntryAssembly(), typeof(log4net.Repository.Hierarchy.Hierarchy));
            log4net.Config.XmlConfigurator.Configure(repo, Config.Log4Net);

            LoggerFactory.Factory = new Log4NetAdapter();

            CloudManager.Settings = new CloudSettings
            {
                TwoFaHandler = LoadHandler(Config.TwoFactorAuthHandler),
                Protocol = options.Protocol,
                UserAgent = ConstructUserAgent(options.UserAgent, Config.DefaultUserAgent),
                SecChUa = ConstructSecChUa(options.SecChUa, Config.DefaultSecChUa),
                CacheListingSec = options.CacheListingSec,
                MaxConnectionCount = options.MaxConnectionCount,
                ListDepth = options.CacheListingDepth,
                AdditionalSpecialCommandPrefix = Config.AdditionalSpecialCommandPrefix,
                DefaultSharedVideoResolution = Config.DefaultSharedVideoResolution,
                UseLocks = options.UseLocks,

                UseDeduplicate = options.UseDeduplicate,
                DeduplicateRules = Config.DeduplicateRules,

                Proxy = ProxyFabric.Get(options.ProxyAddress, options.ProxyUser, options.ProxyPassword),

                DisableLinkManager = options.DisableLinkManager,
                DetectActivityInterval = options.DetectActivityInterval,

                CloudInstanceTimeoutMinutes = options.CloudInstanceTimeoutMinutes,

                Wait100ContinueTimeoutSec = options.Wait100ContinueTimeoutSec,
                WaitResponseTimeoutSec = options.WaitResponseTimeoutSec,
                ReadWriteTimeoutSec = options.ReadWriteTimeoutSec,

                BrowserAuthenticatorUrl = Config.BrowserAuthenticator?.Url,
                BrowserAuthenticatorPassword = Config.BrowserAuthenticator?.Password,
            };

            ShowInfo(options);

            //var z = CloudManager.Settings.TwoFaHandler.Get("zzz", true);

            var httpListener = new HttpListener();
            var httpListenerOptions = new HttpListenerOptions(options);
            try
            {
                foreach (var prefix in httpListenerOptions.Prefixes)
                    httpListener.Prefixes.Add(prefix);
                httpListener.AuthenticationSchemes = httpListenerOptions.AuthenticationScheme;
                httpListener.Start();

                Logger.Info(
                    $"WebDAV server running at {httpListenerOptions.Prefixes.Aggregate(
                        (current, next) => string.Concat(current, ", ", next))}");

                // Start dispatching requests
                var t = DispatchHttpRequestsAsync(httpListener, options.MaxThreadCount);
                t.Wait(CancelToken.Token);

                //do not use console input - it uses 100% CPU when running mono-service in ubuntu
            }
            catch (OperationCanceledException ce) when (ce.CancellationToken.IsCancellationRequested)
            {
                Logger.Info("Canceled");
            }
            catch (HttpListenerException e) when (e.Message.ContainsIgnoreCase("conflicts with an existing"))
            {
                // System.Net.HttpListenerException: 'Failed to listen on prefix 'http://127.0.0.1:12345/'
                // because it conflicts with an existing registration on the machine.'
                Logger.Error(e.Message);
                Logger.Error("Please quit another copy of the program");
            }
            finally
            {
                httpListener.Stop();
            }
        }

        private static string ConstructUserAgent(string fromOptions, string fromConfig)
        {
            if (!string.IsNullOrWhiteSpace(fromOptions))
                return fromOptions;
            if (!string.IsNullOrWhiteSpace(fromConfig))
                return fromConfig;

            Logger.Warn($"Configuration for User-Agent not found, using '{Config.DefaultUserAgentInternal}'");
            return Config.DefaultUserAgentInternal;
        }

        private static string ConstructSecChUa(string fromOptions, string fromConfig)
        {
            if (!string.IsNullOrWhiteSpace(fromOptions))
                return fromOptions;
            if (!string.IsNullOrWhiteSpace(fromConfig))
                return fromConfig;

            Logger.Warn($"Configuration for sec-ch-ua not found, using '{Config.DefaultSecChUaInternal}'");
            return Config.DefaultSecChUaInternal;
        }


        private static ITwoFaHandler LoadHandler(TwoFactorAuthHandlerInfo handlerInfo)
        {
            if (string.IsNullOrEmpty(handlerInfo.Name))
                return null;

            var twoFaHandler = TwoFaHandlers.Get(handlerInfo);
            if (null == twoFaHandler)
                Logger.Error($"Cannot load two-factor auth handler {handlerInfo.Name}");

            return twoFaHandler;
        }

        private static async Task DispatchHttpRequestsAsync(HttpListener httpListener, int maxThreadCount = int.MaxValue)
        {
            // Create a request handler factory that uses basic authentication
            var requestHandlerFactory = new RequestHandlerFactory();

            // Create WebDAV dispatcher
            var homeFolder = new LocalStore(
                isEnabledPropFunc: Config.IsEnabledWebDAVProperty,
                lockingManager: CloudManager.Settings.UseLocks ? new InMemoryLockingManager() : new EmptyLockingManager());
            var webDavDispatcher = new WebDavDispatcher(homeFolder, requestHandlerFactory);

            try
            {
                using var sem = new SemaphoreSlim(maxThreadCount);

                var semclo = sem;
                while (!CancelToken.IsCancellationRequested)
                {
                    var httpListenerContext = await httpListener.GetContextAsync().ConfigureAwait(false);
                    if (httpListenerContext == null)
                        break;

                    HttpListenerBasicIdentity identity = (HttpListenerBasicIdentity)httpListenerContext.User.Identity;
                    IHttpContext httpContext = new HttpBasicContext(httpListenerContext, i => i.Name == identity.Name && i.Password == identity.Password);

                    await semclo.WaitAsync(CancelToken.Token);

                    var _ = Task.Run(async () =>
                    {
                        try
                        {
                            await webDavDispatcher.DispatchRequestAsync(httpContext)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            semclo.Release();
                        }
                    }, CancelToken.Token);
                }
            }
            catch (HttpListenerException) when (CancelToken.IsCancellationRequested)
            {
                Logger.Info("Server stopped");
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Server stopped");
            }
            catch (Exception e)
            {
                Logger.Error("Global exception", e);
            }
        }


        private static void ShowInfo(CommandLineOptions options)
        {
            string title = GetAssemblyAttribute<AssemblyProductAttribute>(a => a.Product);
            string description = GetAssemblyAttribute<AssemblyDescriptionAttribute>(a => a.Description);
            string copyright = GetAssemblyAttribute<AssemblyCopyrightAttribute>(a => a.Copyright);
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            System.Console.WriteLine($"  {title}: {description}");
            System.Console.WriteLine($"  v.{version}");
            System.Console.WriteLine($"  {copyright}");

            Logger.Info($"OS Version: {Environment.OSVersion}");
            Logger.Info($"CLR: {GetFrameworkDescription()}");
            Logger.Info($"User interactive: {Environment.UserInteractive}");
            Logger.Info($"Version: {version}");
            Logger.Info($"Using proxy: {options.ProxyAddress}");
            Logger.Info($"Max listening threads count: {options.MaxThreadCount}");
            Logger.Info($"Max cloud connections count: {options.MaxConnectionCount}");
            Logger.Info($"Cloud server response timeout: {options.WaitResponseTimeoutSec} sec");
            Logger.Info($"Cloud download/upload timeout: {options.ReadWriteTimeoutSec} sec");
            Logger.Info($"Wait for 100-Continue timeout: {options.Wait100ContinueTimeoutSec} sec");
            Logger.Info($"Cloud & protocol: defined by login and the rest parameters");
            Logger.Info($"Cloud instance (server+login) expiration timeout: {options.CloudInstanceTimeoutMinutes} min");
            Logger.Info($"Track changes on Yandex.Disk made behind the Emulator: " +
                $"{(options.DetectActivityInterval == 0 ? "disabled" : $"every {options.DetectActivityInterval} sec")}");
            Logger.Info($"Folder cache expiration timeout: {options.CacheListingSec} sec");
            Logger.Info($"List query folder depth: {options.CacheListingDepth}");
            Logger.Info($"Use locks: {options.UseLocks}");
            Logger.Info($"Support links in /item.links.wdmrc: {(!options.DisableLinkManager)}");
            Logger.Info($"Use deduplicate: {options.UseDeduplicate}");
            Logger.Info($"Start as service: {options.ServiceRun}");
        }

        private static string GetAssemblyAttribute<T>(Func<T, string> value) where T : Attribute
        {
            T attribute = (T)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(T));
            return null == attribute ? null : value.Invoke(attribute);
        }


        private static string GetFrameworkDescription()
        {
            // detect .NET Core & .NET 5
            var assembly = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly;
            var assemblyPath = assembly.Location.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            //var assemblyPath = assembly.CodeBase.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            int netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
            if (netCoreAppIndex > 0 && netCoreAppIndex < assemblyPath.Length - 2)
            {
                bool parsed = Version.TryParse(assemblyPath[netCoreAppIndex + 1], out var version);

                return (parsed && version.Major >= 5
                    ? ".NET "
                    : ".NET Core ") + assemblyPath[netCoreAppIndex + 1];
            }

            // detect .NET Mono
            Type type = Type.GetType("Mono.Runtime");
            if (type == null)
                return string.Concat(".NET Framework ", Environment.Version);

            MethodInfo displayName = type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
            return displayName != null
                ? "Mono " + displayName.Invoke(null, null)
                : "unknown";

            // .NET Framework, yep?
        }
    }
}
