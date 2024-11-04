using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud.WebV2.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;
using static YaR.Clouds.Cloud;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebV2;

class WebV2RequestRepo : MailRuBaseRepo, IRequestRepo
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(WebV2RequestRepo));

    private readonly SemaphoreSlim _connectionLimiter;

    public sealed override HttpCommonSettings HttpSettings { get; } = new()
    {
        ClientId = string.Empty,
        BaseDomain = "https://cloud.mail.ru"
    };

    public WebV2RequestRepo(CloudSettings settings, IBasicCredentials credentials, AuthCodeRequiredDelegate onAuthCodeRequired)
        : base(credentials)
    {
        ServicePointManager.DefaultConnectionLimit = int.MaxValue;

        // required for Windows 7 breaking connection
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;

        _connectionLimiter = new SemaphoreSlim(settings.MaxConnectionCount);
        HttpSettings.UserAgent = settings.UserAgent;
        HttpSettings.CloudSettings = settings;
        HttpSettings.Proxy = settings.Proxy;

        Auth = new WebAuth(_connectionLimiter, HttpSettings, credentials, onAuthCodeRequired);

        _bannedShards = new Cached<List<ShardInfo>>(_ => [], _ => TimeSpan.FromMinutes(2));

        _cachedShards = new Cached<Dictionary<ShardType, ShardInfo>>(
            _ => new ShardInfoRequest(HttpSettings, Auth).MakeRequestAsync(_connectionLimiter).Result.ToShardInfo(),
            _ => TimeSpan.FromSeconds(ShardsExpiresInSec));
    }





    private readonly Cached<Dictionary<ShardType, ShardInfo>> _cachedShards;
    private readonly Cached<List<ShardInfo>> _bannedShards;
    private const int ShardsExpiresInSec = 30 * 60;


    //public HttpWebRequest UploadRequest(File file, UploadMultipartBoundary boundary)
    //{
    //    var shard = GetShardInfo(ShardType.Upload).Result;

    //    var url = new Uri($"{shard.Url}?cloud_domain=2&{Authenticator.Login}");

    //    var result = new UploadRequest(url.OriginalString, file, Authenticator, HttpSettings);
    //    return result;
    //}

    public Stream GetDownloadStream(File file, long? start = null, long? end = null)
    {

        CustomDisposable<HttpWebResponse> ResponseGenerator(long instart, long inend, File file)
        {
            HttpWebRequest request = new DownloadRequest(file, instart, inend, Auth, HttpSettings, _cachedShards);
            var response = (HttpWebResponse)request.GetResponse();

            return new CustomDisposable<HttpWebResponse>
            {
                Value = response,
                OnDispose = () =>
                {
                    //_shardManager.DownloadServersPending.Free(downServer);
                    //watch.Stop();
                    //Logger.Debug($"HTTP:{request.Method}:{request.RequestUri.AbsoluteUri} ({watch.Elapsed.Milliseconds} ms)");
                }
            };
        }

        var stream = new DownloadStream(ResponseGenerator, file, start, end);
        return stream;
    }

    //public HttpWebRequest DownloadRequest(long instart, long inend, File file, ShardInfo shard)
    //{
    //    string downloadkey = string.Empty;
    //    if (shard.Type == ShardType.WeblinkGet)
    //        downloadkey = Authenticator.DownloadToken;

    //    string url = shard.Type == ShardType.Get
    //        ? $"{shard.Url}{Uri.EscapeDataString(file.FullPath)}"
    //        : $"{shard.Url}{new Uri(ConstSettings.PublishFileLink + file.PublicLink).PathAndQuery.Remove(0, "/public".Length)}?key={downloadkey}";

    //    var request = (HttpWebRequest)WebRequest.Create(url);

    //    request.Headers.Add("Accept-Ranges", "bytes");
    //    request.AddRange(instart, inend);
    //    request.Proxy = HttpSettings.Proxy;
    //    request.CookieContainer = Authenticator.Cookies;
    //    request.Method = "GET";
    //    request.ContentType = MediaTypeNames.Application.Octet;
    //    request.Accept = "*/*";
    //    request.UserAgent = HttpSettings.UserAgent;
    //    request.AllowReadStreamBuffering = false;

    //    request.Timeout = 15 * 1000;

    //    return request;
    //}


    //public void BanShardInfo(ShardInfo banShard)
    //{
    //    if (!_bannedShards.Value.Any(bsh => bsh.Type == banShard.Type && bsh.Url == banShard.Url))
    //    {
    //        Logger.Warn($"Shard {banShard.Url} temporarily banned");
    //        _bannedShards.Value.Add(banShard);
    //    }
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
            var ishards = await Task.Run(() => _cachedShards.Value);
            var ishard = ishards[shardType];
            var banned = _bannedShards.Value;
            if (banned.All(bsh => bsh.Url != ishard.Url))
            {
                if (refreshed) Auth.ExpireDownloadToken();
                return ishard;
            }
            _cachedShards.Expire();
            refreshed = true;
        }

        Logger.Error("Cannot get working shard.");

        var shards = await Task.Run(() => _cachedShards.Value);
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
        var req = await new MoveRequest(HttpSettings, Auth, sourceFullPath, destinationPath).MakeRequestAsync(_connectionLimiter);
        var res = req.ToCopyResult();
        return res;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="path"></param>
    /// <param name="offset"></param>
    /// <param name="limit"></param>
    /// <param name="depth">Not applicable here, always = 1</param>
    /// <returns></returns>
    public async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
    {

        FolderInfoResult dataRes;
        try
        {
            dataRes = await new FolderInfoRequest(HttpSettings, Auth, path, offset, limit)
                .MakeRequestAsync(_connectionLimiter);
        }
        catch (WebException e) when (e.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound })
        {
            return null;
        }

        Cloud.ItemType itemType;
        if (null == path.Link || path.Link.ItemType == Cloud.ItemType.Unknown)
            itemType = dataRes.Body.Home == path.Path ||
                       WebDavPath.PathEquals("/" + dataRes.Body.Weblink, path.Path)
                //datares.body.list.Any(fi => "/" + fi.weblink == path)
                ? Cloud.ItemType.Folder
                : Cloud.ItemType.File;
        else
            itemType = path.Link.ItemType;


        var entry = itemType == Cloud.ItemType.File
            ? (IEntry)dataRes.ToFile(
                PublicBaseUrlDefault,
                home: WebDavPath.Parent(path.Path ?? string.Empty),
                ulink: path.Link,
                fileName: path.Link == null ? WebDavPath.Name(path.Path) : path.Link.OriginalName,
                nameReplacement: path.Link?.IsLinkedToFileSystem ?? true ? WebDavPath.Name(path.Path) : null)
            : (IEntry)dataRes.ToFolder(PublicBaseUrlDefault, path.Path, path.Link);

        return entry;
    }

    public async Task<FolderInfoResult> ItemInfo(RemotePath path, int offset = 0, int limit = int.MaxValue)
    {
        var req = await new ItemInfoRequest(HttpSettings, Auth, path, offset, limit)
            .MakeRequestAsync(_connectionLimiter);
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
        return res;
    }

    public async Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath = null)
    {
        var req = await new UnpublishRequest(HttpSettings, Auth, publicLink.OriginalString).MakeRequestAsync(_connectionLimiter);
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
        var req = await new RenameRequest(HttpSettings, Auth, fullPath, newName).MakeRequestAsync(_connectionLimiter);
        var res = req.ToRenameResult();
        return res;
    }

    public Dictionary<ShardType, ShardInfo> GetShardInfo1()
    {
        return new ShardInfoRequest(HttpSettings, Auth).MakeRequestAsync(_connectionLimiter).Result.ToShardInfo();
    }

    public IEnumerable<PublicLinkInfo> GetShareLinks(string fullPath)
    {
        throw new NotImplementedException("WebV2 GetShareLink not implemented");
    }

    public void CleanTrash()
    {
        throw new NotImplementedException();
    }

    public async Task<CreateFolderResult> CreateFolder(string path)
    {
        return (await new CreateFolderRequest(HttpSettings, Auth, path).MakeRequestAsync(_connectionLimiter))
            .ToCreateFolderResult();
    }

    public async Task<AddFileResult> AddFile(string fileFullPath, IFileHash fileHash, FileSize fileSize, DateTime dateTime, ConflictResolver? conflictResolver)
    {
        var hash = fileHash.Hash.Value;

        var res = await new CreateFileRequest(HttpSettings, Auth, fileFullPath, hash, fileSize, conflictResolver)
            .MakeRequestAsync(_connectionLimiter);

        return res.ToAddFileResult();
    }

    public async Task<CheckUpInfo> DetectOutsideChanges() => await Task.FromResult<CheckUpInfo>(null);
}
