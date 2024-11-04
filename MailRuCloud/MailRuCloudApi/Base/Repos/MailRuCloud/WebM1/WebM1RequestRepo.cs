using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;
using CreateFolderRequest = YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.CreateFolderRequest;
using MoveRequest = YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.MoveRequest;
using static YaR.Clouds.Cloud;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebM1;

/// <summary>
/// Part of WebM1 protocol.
/// Not usable.
/// </summary>
abstract class WebM1RequestRepo : MailRuBaseRepo, IRequestRepo
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(WebM1RequestRepo));
    private readonly AuthCodeRequiredDelegate _onAuthCodeRequired;

    private readonly SemaphoreSlim _connectionLimiter;

    protected ShardManager ShardManager { get; private set; }

    protected IRequestRepo AnonymousRepo => throw new NotImplementedException();

    public sealed override HttpCommonSettings HttpSettings { get; } = new()
    {
        ClientId = "cloud-win",
        UserAgent = "CloudDiskOWindows 17.12.0009 beta WzBbt1Ygbm",
        BaseDomain = "https://cloud.mail.ru"
    };

    protected WebM1RequestRepo(CloudSettings settings, IWebProxy proxy,
        IBasicCredentials credentials, AuthCodeRequiredDelegate onAuthCodeRequired)
        : base(credentials)
    {
        ServicePointManager.DefaultConnectionLimit = int.MaxValue;

        // required for Windows 7 breaking connection
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;

        _connectionLimiter = new SemaphoreSlim(settings.MaxConnectionCount);
        ShardManager = new ShardManager(_connectionLimiter, this);
        HttpSettings.Proxy = proxy;
        HttpSettings.CloudSettings = settings;
        _onAuthCodeRequired = onAuthCodeRequired;

        Auth = new OAuth(_connectionLimiter, HttpSettings, credentials, onAuthCodeRequired);

        CachedSharedList = new Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>>(_ =>
            {
                var z = GetShareListInner().Result;

                var res = z.Body.List
                    .ToDictionary(
                        fik => fik.Home,
                        fiv => Enumerable.Repeat(new PublicLinkInfo(PublicBaseUrlDefault + fiv.Weblink), 1));

                return res;
            },
            _ => TimeSpan.FromSeconds(30));
    }



    public Stream GetDownloadStream(File file, long? start = null, long? end = null)
    {
        var istream = GetDownloadStreamInternal(file, start, end);
        return istream;
    }

    private DownloadStream GetDownloadStreamInternal(File afile, long? start = null, long? end = null)
    {
        bool isLinked = !afile.PublicLinks.IsEmpty;

        Cached<ServerRequestResult> downServer = null;
        var pendingServers = isLinked
            ? ShardManager.WebLinkDownloadServersPending
            : ShardManager.DownloadServersPending;
        Stopwatch watch = new Stopwatch();

        HttpWebRequest request = null;
        CustomDisposable<HttpWebResponse> ResponseGenerator(long instart, long inend, File file)
        {
            var resp = Retry.Do(() =>
            {
                downServer = pendingServers.Next(downServer);

                string url = (isLinked
                        ? $"{downServer.Value.Url}{WebDavPath.EscapeDataString(file.PublicLinks.Values.FirstOrDefault()?.Uri.PathAndQuery)}"
                        : $"{downServer.Value.Url}{Uri.EscapeDataString(file.FullPath.TrimStart('/'))}") +
                    $"?client_id={HttpSettings.ClientId}&token={Auth.AccessToken}";
                var uri = new Uri(url);

#pragma warning disable SYSLIB0014 // Type or member is obsolete
                request = (HttpWebRequest)WebRequest.Create(uri.OriginalString);
#pragma warning restore SYSLIB0014 // Type or member is obsolete

                request.AddRange(instart, inend);
                request.Proxy = HttpSettings.Proxy;
                request.CookieContainer = Auth.Cookies;
                request.Method = "GET";
                request.Accept = "*/*";
                request.UserAgent = HttpSettings.UserAgent;
                request.Host = uri.Host;
                request.AllowWriteStreamBuffering = false;

                if (isLinked)
                {
                    request.Headers.Add("Accept-Ranges", "bytes");
                    request.ContentType = MediaTypeNames.Application.Octet;
                    request.Referer = $"{HttpSettings.BaseDomain}/home/{Uri.EscapeDataString(file.Path)}";
                    request.Headers.Add("Origin", HttpSettings.BaseDomain);
                }

                request.Timeout = 15 * 1000;
                request.ReadWriteTimeout = 15 * 1000;

                watch.Start();
                var response = (HttpWebResponse)request.GetResponse();
                return new CustomDisposable<HttpWebResponse>
                {
                    Value = response,
                    OnDispose = () =>
                    {
                        pendingServers.Free(downServer);
                        watch.Stop();
                        Logger.Debug($"HTTP:{request.Method}:{request.RequestUri.AbsoluteUri} ({watch.Elapsed.Milliseconds} ms)");
                    }
                };
            },
            exception =>
                exception is WebException { Response: HttpWebResponse { StatusCode: HttpStatusCode.NotFound } },
            exception =>
            {
                pendingServers.Free(downServer);
                Logger.Warn($"Retrying HTTP:{request.Method}:{request.RequestUri.AbsoluteUri} on exception {exception.Message}");
            },
            TimeSpan.FromSeconds(1), 2);

            return resp;
        }

        var stream = new DownloadStream(ResponseGenerator, afile, start, end);
        return stream;
    }


    //public HttpWebRequest UploadRequest(File file, UploadMultipartBoundary boundary)
    //{
    //    var shard = GetShardInfo(ShardType.Upload).Result;
    //    var url = new Uri($"{shard.Url}?client_id={HttpSettings.ClientId}&token={Authenticator.AccessToken}");

    //    var request = (HttpWebRequest)WebRequest.Create(url);
    //    request.Proxy = HttpSettings.Proxy;
    //    request.CookieContainer = Authenticator.Cookies;
    //    request.Method = "PUT";
    //    request.ContentLength = file.OriginalSize;
    //    request.Accept = "*/*";
    //    request.UserAgent = HttpSettings.UserAgent;
    //    request.AllowWriteStreamBuffering = false;

    //    request.Timeout = 15 * 1000;
    //    request.ReadWriteTimeout = 15 * 1000;
    //    //request.ServicePoint.ConnectionLimit = int.MaxValue;

    //    return request;
    //}

    /// <summary>
    /// Get shard info that to do post get request. Can be use for anonymous user.
    /// </summary>
    /// <param name="shardType">Shard type as numeric type.</param>
    /// <returns>Shard info.</returns>
    public override async Task<ShardInfo> GetShardInfo(ShardType shardType)
    {
        bool refreshed = false;
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(80 * i);
            var ishards = await Task.Run(() => ShardManager.CachedShards.Value);
            var ishard = ishards[shardType];
            var banned = ShardManager.BannedShards.Value;
            if (banned.All(bsh => bsh.Url != ishard.Url))
            {
                if (refreshed) Auth.ExpireDownloadToken();
                return ishard;
            }
            ShardManager.CachedShards.Expire();
            refreshed = true;
        }

        Logger.Error("Cannot get working shard.");

        var shards = await Task.Run(() => ShardManager.CachedShards.Value);
        var shard = shards[shardType];
        return shard;
    }

    public async Task<CloneItemResult> CloneItem(string fromUrl, string toPath)
    {
        var req = await new CloneItemRequest(HttpSettings, Auth, fromUrl, toPath).MakeRequestAsync(_connectionLimiter);
        var res = req.ToCloneItemResult();
        return res;
    }

    public async Task<CopyResult> Copy(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
    {
        var req = await new CopyRequest(HttpSettings, Auth, sourceFullPath, destinationPath, conflictResolver).MakeRequestAsync(_connectionLimiter);
        var res = req.ToCopyResult();
        return res;
    }

    public async Task<CopyResult> Move(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
    {
        //var req = await new MoveRequest(HttpSettings, Authenticator, sourceFullPath, destinationPath).MakeRequestAsync(_connectionLimiter);
        //var res = req.ToCopyResult();
        //return res;

        var req = await new MoveRequest(HttpSettings, Auth, ShardManager.MetaServer.Url, sourceFullPath, destinationPath)
            .MakeRequestAsync(_connectionLimiter);

        var res = req.ToCopyResult(WebDavPath.Name(destinationPath));
        return res;

    }

    public async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
    {
        if (Credentials.IsAnonymous)
            return await AnonymousRepo.FolderInfo(path, offset, limit);

        if (!path.IsLink && depth > 1)
            return await FolderInfo(path, depth);

        FolderInfoResult datares;
        try
        {
            datares = await new FolderInfoRequest(HttpSettings, Auth, path, offset, limit)
                .MakeRequestAsync(_connectionLimiter);
        }
        catch (WebException e) when (e.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound })
        {
            return null;
        }

        Cloud.ItemType itemType;

        //TODO: subject to refact, bad-bad-bad
        if (null == path.Link || path.Link.ItemType == Cloud.ItemType.Unknown)
            itemType = datares.Body.Home == path.Path ||
                       WebDavPath.PathEquals("/" + datares.Body.Weblink, path.Path)
                ? Cloud.ItemType.Folder
                : Cloud.ItemType.File;
        else
            itemType = path.Link.ItemType;


        var entry = itemType == Cloud.ItemType.File
            ? (IEntry)datares.ToFile(
                PublicBaseUrlDefault,
                home: WebDavPath.Parent(path.Path ?? string.Empty),
                ulink: path.Link,
                fileName: path.Link == null ? WebDavPath.Name(path.Path) : path.Link.OriginalName,
                nameReplacement: path.Link?.IsLinkedToFileSystem ?? true ? WebDavPath.Name(path.Path) : null)
            : (IEntry)datares.ToFolder(PublicBaseUrlDefault, path.Path, path.Link);

        return entry;
    }

    public async Task<FolderInfoResult> ItemInfo(RemotePath path, int offset = 0, int limit = int.MaxValue)
    {
        var req = await new ItemInfoRequest(HttpSettings, Auth, path, offset, limit).MakeRequestAsync(_connectionLimiter);
        return req;
    }

    public async Task<AccountInfoResult> AccountInfo()
    {
        var req = await new AccountInfoRequest(HttpSettings, Auth).MakeRequestAsync(_connectionLimiter);
        var res = req.ToAccountInfo();
        return res;
    }

    public async Task<PublishResult> Publish(string fullPath)
    {
        var req = await new PublishRequest(HttpSettings, Auth, fullPath).MakeRequestAsync(_connectionLimiter);
        var res = req.ToPublishResult();

        if (res.IsSuccess)
        {
            CachedSharedList.Value[fullPath] = new List<PublicLinkInfo> { new(PublicBaseUrlDefault + res.Url) };
        }

        return res;
    }

    public async Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath = null)
    {
        foreach (var item in CachedSharedList.Value
            .Where(kvp => kvp.Value.Any(u => u.Uri.Equals(publicLink))).ToList())
        {
            CachedSharedList.Value.Remove(item.Key);
        }

        var req = await new UnpublishRequest(this, HttpSettings, Auth, publicLink.OriginalString).MakeRequestAsync(_connectionLimiter);
        var res = req.ToUnpublishResult();
        return res;
    }

    public async Task<RemoveResult> Remove(string fullPath)
    {
        var req = await new RemoveRequest(HttpSettings, Auth, fullPath).MakeRequestAsync(_connectionLimiter);
        var res = req.ToRemoveResult();
        return res;
    }

    public async Task<RenameResult> Rename(string fullPath, string newName)
    {
        //var req = await new RenameRequest(HttpSettings, Authenticator, fullPath, newName).MakeRequestAsync(_connectionLimiter);
        //var res = req.ToRenameResult();
        //return res;

        string newFullPath = WebDavPath.Combine(WebDavPath.Parent(fullPath), newName);
        var req = await new MoveRequest(HttpSettings, Auth, ShardManager.MetaServer.Url, fullPath, newFullPath)
            .MakeRequestAsync(_connectionLimiter);

        var res = req.ToRenameResult();
        return res;
    }

    public Dictionary<ShardType, ShardInfo> GetShardInfo1()
    {
        return Auth.IsAnonymous
            ? new WebV2.Requests
                 .ShardInfoRequest(HttpSettings, Auth).MakeRequestAsync(_connectionLimiter).Result.ToShardInfo()
            : new ShardInfoRequest(HttpSettings, Auth).MakeRequestAsync(_connectionLimiter).Result.ToShardInfo();
    }


    public Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> CachedSharedList { get; }

    private async Task<FolderInfoResult> GetShareListInner()
    {
        var res = await new SharedListRequest(HttpSettings, Auth)
            .MakeRequestAsync(_connectionLimiter);

        return res;
    }

    public IEnumerable<PublicLinkInfo> GetShareLinks(string path)
    {
        if (!CachedSharedList.Value.TryGetValue(path, out var links))
            yield break;

        foreach (var link in links)
            yield return link;
    }

    public void CleanTrash()
    {
        throw new NotImplementedException();
    }

    public async Task<CreateFolderResult> CreateFolder(string path)
    {
        //return (await new CreateFolderRequest(HttpSettings, Authenticator, path).MakeRequestAsync())
        //    .ToCreateFolderResult();

        var folderReqest = await new CreateFolderRequest(HttpSettings, Auth, ShardManager.MetaServer.Url, path)
            .MakeRequestAsync(_connectionLimiter);

        return folderReqest.ToCreateFolderResult();
    }

    public Task<AddFileResult> AddFile(string fileFullPath, IFileHash fileHash, FileSize fileSize, DateTime dateTime, ConflictResolver? conflictResolver)
    {
        throw new NotImplementedException();
    }

    public async Task<CheckUpInfo> DetectOutsideChanges() => await Task.FromResult<CheckUpInfo>(null);
}
