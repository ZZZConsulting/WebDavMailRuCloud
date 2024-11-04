using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests;
using YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.Types;
using YaR.Clouds.Base.Repos.MailRuCloud.WebBin.Requests;
using YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;

using AnonymousRepo = YaR.Clouds.Base.Repos.MailRuCloud.WebV2.WebV2RequestRepo;
using AccountInfoRequest = YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests.AccountInfoRequest;
using CreateFolderRequest = YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.CreateFolderRequest;
using MoveRequest = YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.MoveRequest;
using static YaR.Clouds.Cloud;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebBin
{
    /// <summary>
    /// Combination of WebM1 and Mobile protocols
    /// </summary>
    class WebBinRequestRepo : MailRuBaseRepo, IRequestRepo
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(WebBinRequestRepo));

        private readonly SemaphoreSlim _connectionLimiter;
        private readonly AuthCodeRequiredDelegate _onAuthCodeRequired;

        protected ShardManager ShardManager { get; private set; }

        protected IRequestRepo AnonymousRepo => _anonymousRepo ??=
            new AnonymousRepo(HttpSettings.CloudSettings, Credentials, _onAuthCodeRequired);
        private IRequestRepo _anonymousRepo;


        public sealed override HttpCommonSettings HttpSettings { get; } = new()
        {
            //ClientId = "cloud-android"
            ClientId = "cloud-win",
            BaseDomain = "https://cloud.mail.ru"
        };

        public WebBinRequestRepo(CloudSettings settings, IBasicCredentials credentials, AuthCodeRequiredDelegate onAuthCodeRequired)
            : base(credentials)
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;

            // required for Windows 7 breaking connection
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;

            _connectionLimiter = new SemaphoreSlim(settings.MaxConnectionCount);

            HttpSettings.CloudSettings = settings;
            HttpSettings.UserAgent = settings.UserAgent;
            HttpSettings.Proxy = settings.Proxy;

            _onAuthCodeRequired = onAuthCodeRequired;
            Auth = new OAuth(_connectionLimiter, HttpSettings, credentials, onAuthCodeRequired);

            ShardManager = new ShardManager(_connectionLimiter, this);
        }


        public Stream GetDownloadStream(File file, long? start = null, long? end = null)
        {
            var istream = GetDownloadStreamInternal(file, start, end);
            return istream;
        }

        private DownloadStream GetDownloadStreamInternal(File file, long? start = null, long? end = null)
        {
            bool isLinked = !file.PublicLinks.IsEmpty;

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

                    request = new DownloadRequest(HttpSettings, Auth, file, instart, inend, downServer.Value.Url, PublicBaseUrls);

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

            var stream = new DownloadStream(ResponseGenerator, file, start, end);
            return stream;
        }

        /// <summary>
        /// Get shard info that to do post get request. Can be use for anonymous user.
        /// </summary>
        /// <param name="shardType">Shard type as numeric type.</param>
        /// <returns>Shard info.</returns>
        public override async Task<ShardInfo> GetShardInfo(ShardType shardType)
        {
            //TODO: rewrite ShardManager
            if (shardType == ShardType.Upload) return ShardManager.UploadServer.Value;

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
            //var req = await new MoveRequest(HttpSettings, Auth, sourceFullPath, destinationPath).MakeRequestAsync(_connectionLimiter);
            //var res = req.ToCopyResult();
            //return res;

            var req = await new MoveRequest(HttpSettings, Auth, ShardManager.MetaServer.Url, sourceFullPath, destinationPath)
                .MakeRequestAsync(_connectionLimiter);

            var res = req.ToCopyResult(WebDavPath.Name(destinationPath));
            return res;

        }

        private async Task<IEntry> FolderInfo(string path, int depth = 1)
        {
            try
            {
                ListRequest.Result dataRes =
                    await new ListRequest(HttpSettings, Auth, ShardManager.MetaServer.Url, path, depth)
                        .MakeRequestAsync(_connectionLimiter);

                // если файл разбит или зашифрован - то надо взять все куски
                // в протоколе V2 на запрос к файлу сразу приходит листинг каталога, в котором он лежит
                // здесь (протокол Bin) приходит информация именно по указанному файлу
                // поэтому вот такой костыль с двойным запросом
                //TODO: переделать двойной запрос к файлу
                if (dataRes.Item is FsFile { Size: < 2048 })
                {
                    string name = WebDavPath.Name(path);
                    path = WebDavPath.Parent(path);

                    dataRes = await new ListRequest(HttpSettings, Auth, ShardManager.MetaServer.Url, path, 1)
                        .MakeRequestAsync(_connectionLimiter);

                    var folder = dataRes.ToFolder();

                    return folder.Descendants.FirstOrDefault(f => f.Name == name);
                }
                return dataRes.ToEntry();
            }
            catch (RequestException re) when (re.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (WebException e) when (e.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound })
            {
                return null;
            }
        }

        public async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
        {
            if (Credentials.IsAnonymous)
                return await AnonymousRepo.FolderInfo(path, offset, limit);

            if (!path.IsLink && depth > 1)
                return await FolderInfo(path.Path, depth);

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

            //TODO: subject to refact, bad-bad-bad
            if (!path.IsLink || path.Link.ItemType == Cloud.ItemType.Unknown)
                itemType = dataRes.Body.Home == path.Path ||
                           WebDavPath.PathEquals("/" + dataRes.Body.Weblink, path.Path)
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
                    nameReplacement: path.Link?.IsLinkedToFileSystem ?? true ? WebDavPath.Name(path.Path) : path.Link.Name)
                : (IEntry)dataRes.ToFolder(PublicBaseUrlDefault, path.Path, path.Link);

            if (limit == int.MaxValue && entry is Folder fld)
                fld.IsChildrenLoaded = limit == int.MaxValue;

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
                CachedSharedList.Value[fullPath] = [new PublicLinkInfo(PublicBaseUrlDefault + res.Url)];
            }

            return res;
        }

        public async Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath)
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
            //var req = await new RenameRequest(HttpSettings, Authent, fullPath, newName).MakeRequestAsync(_connectionLimiter);
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


        public Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> CachedSharedList
        {
            get
            {
                return _cachedSharedList ??= new Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>>(_ =>
                    {
                        var z = GetShareListInner().Result;

                        var res = z.Body.List
                            .ToDictionary(
                                fik => fik.Home,
                                fiv => Enumerable.Repeat(new PublicLinkInfo(PublicBaseUrlDefault + fiv.Weblink),
                                    1));

                        return res;
                    },
                    _ => TimeSpan.FromSeconds(30));
            }
        }
        private Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> _cachedSharedList;

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
            /*
             * В названии папок нельзя использовать символы «" * / : < > ? \ |».
             * Также название не может состоять только из точки «.» или из двух точек «..»
             */
            path = StripBadSymbols(path);

            //return (await new CreateFolderRequest(HttpSettings, Authenticator, path).MakeRequestAsync())
            //    .ToCreateFolderResult();

            return (await new CreateFolderRequest(HttpSettings, Auth, ShardManager.MetaServer.Url, path).MakeRequestAsync(_connectionLimiter))
                .ToCreateFolderResult();
        }

        public async Task<AddFileResult> AddFile(string fileFullPath, IFileHash fileHash, FileSize fileSize, DateTime dateTime, ConflictResolver? conflictResolver)
        {
            /*
             * В названии папок нельзя использовать символы «" * / : < > ? \ |».
             * Также название не может состоять только из точки «.» или из двух точек «..»
             */
            fileFullPath = StripBadSymbols(fileFullPath);

            //var res = await new CreateFileRequest(Proxy, Authenticator, fileFullPath, fileHash, fileSize, conflictResolver)
            //    .MakeRequestAsync(_connectionLimiter);
            //return res.ToAddFileResult();

            //using Mobile request because of supporting file modified time

            //TODO: refact, make mixed repo
            var req = await new MobAddFileRequest(HttpSettings, Auth, ShardManager.MetaServer.Url, fileFullPath, fileHash.Hash.Value, fileSize, dateTime, conflictResolver)
                .MakeRequestAsync(_connectionLimiter);

            var res = req.ToAddFileResult();
            return res;
        }

        public string StripBadSymbols(string fullPath)
        {
            /*
             * В названии папок нельзя использовать символы «" * / : < > ? \ |».
             * Также название не может состоять только из точки «.» или из двух точек «..»
             */
            string name = WebDavPath.Name(fullPath);
            string newName = name
                .Replace("*", "\u2022") // •
                .Replace(":", "\u205e") // ⁞
                .Replace("<", "\u00ab") // «
                .Replace(">", "\u00bb") // »
                .Replace("?", "\u203d") // ‽
                .Replace("|", "\u2502") // │
                .Replace("/", "~")
                .Replace("\\", "~")
                ;
            return name != newName
                ? WebDavPath.Combine(WebDavPath.Parent(fullPath), newName)
                : name;
        }

        public async Task<CheckUpInfo> DetectOutsideChanges() => await Task.FromResult<CheckUpInfo>(null);
    }
}
