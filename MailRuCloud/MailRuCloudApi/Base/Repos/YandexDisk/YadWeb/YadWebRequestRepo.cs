using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models.Media;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;
using Stream = System.IO.Stream;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb;

internal class YadWebRequestRepo : IRequestRepo
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadWebRequestRepo));

    protected readonly SemaphoreSlim _connectionLimiter;

    private const int OperationStatusCheckRetryTimeoutMinutes = 5;

    protected readonly TimeSpan OperationStatusCheckRetryTimeout = TimeSpan.FromMinutes(OperationStatusCheckRetryTimeoutMinutes);

    protected const int OperationStatusCheckIntervalMs = 300;
    protected const int OperationStatusCheckRetryCount = 8;

    protected readonly Credentials _credentials;

    public YadWebRequestRepo(CloudSettings settings, IWebProxy proxy, Credentials credentials)
    {
        _connectionLimiter = new SemaphoreSlim(settings.MaxConnectionCount);

        HttpSettings = new()
        {
            UserAgent = settings.UserAgent,
            CloudSettings = settings,
            Proxy = proxy,
            BaseDomain = "https://disk.yandex.ru"
        };

        _credentials = credentials;

        ServicePointManager.DefaultConnectionLimit = int.MaxValue;

        // required for Windows 7 breaking connection
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;
    }

    protected async Task<Dictionary<string, IEnumerable<PublicLinkInfo>>> GetShareListInner()
    {
        var folderInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                   .Get(new YadModelV2FolderInfo("/", "/published"));

        CheckForError(folderInfo);

        var res = folderInfo.FolderInfo.Resources
            .Where(it => !string.IsNullOrEmpty(it.Meta?.UrlShort))
            .ToDictionary(
                it => it.Path.Remove(0, "/disk".Length),
                it => Enumerable.Repeat(new PublicLinkInfo("short", it.Meta.UrlShort), 1));

        return res;
    }

    public IAuth Auth => CachedAuth.Value;

    public YadWebAuth YadAuth => CachedAuth.Value;

    protected Cached<YadWebAuth> CachedAuth
        => _cachedAuth ??= new Cached<YadWebAuth>(_ => new YadWebAuth(_connectionLimiter, HttpSettings, _credentials),
                                                  _ => TimeSpan.FromHours(23));

    protected Cached<YadWebAuth> _cachedAuth;

    public Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> CachedSharedList
        => _cachedSharedList ??= new Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>>(
            _ =>
                {
                    var res = GetShareListInner().Result;
                    return res;
                },
                _ => TimeSpan.FromSeconds(30));

    protected Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> _cachedSharedList;


    public HttpCommonSettings HttpSettings { get; private set; }

    public static string CheckForError(YadModelV2 modelV2, bool throwException = true)
    {
        if (modelV2 is null)
            throw new ArgumentNullException();

        var errors = modelV2.Errors ?? [];

        if (errors.Count > 0)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var error in errors)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append("[Code=");
                sb.Append(error.Error.Code);
                sb.Append(", Type=");
                sb.Append(error.Error.Type);
                sb.Append(", StatusCode=");
                sb.Append(error.Error.StatusCode);
                sb.Append(", Title=");
                sb.Append(error.Error.Title);
                sb.Append(']');
            }
            sb.Insert(0, "The cloud server returned the following error(s): ");

            if (throwException)
                throw new IOException(sb.ToString());

            return sb.ToString();
        }

        return null;
    }


    public virtual Stream GetDownloadStream(File aFile, long? start = null, long? end = null)
    {
        CustomDisposable<HttpWebResponse> ResponseGenerator(long instart, long inend, File file)
        {
            //var urlData = new YadGetResourceUrlRequest(HttpSettings, Authenticator, file.FullPath)
            //    .MakeRequestAsync(_connectionLimiter)
            //    .Result;
            string url = null;
            if (file.DownloadUrlCache == null ||
                file.DownloadUrlCacheExpirationTime <= DateTime.Now)
            {
                var task = new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                               .Get(new YadModelV2GetResourceDownloadUrl(file.FullPath));

                CheckForError(task.Result);

                var itemInfo = task.Result.DownloadInfo;

                url = "https:" + itemInfo.File;

                file.DownloadUrlCache = url;
                file.DownloadUrlCacheExpirationTime = DateTime.Now.AddMinutes(1);
            }
            else
            {
                url = file.DownloadUrlCache;
            }
            HttpWebRequest request = new YadDownloadRequest(HttpSettings, YadAuth, url, instart, inend);
            var response = (HttpWebResponse)request.GetResponse();

            return new CustomDisposable<HttpWebResponse>
            {
                Value = response,
                OnDispose = () => { }
            };
        }

        if (start.HasValue || end.HasValue)
            Logger.Debug($"Download:  {aFile.FullPath} [{start}-{end}]");
        else
            Logger.Debug($"Download:  {aFile.FullPath}");

        var stream = new DownloadStream(ResponseGenerator, aFile, start, end);
        return stream;
    }

    //public HttpWebRequest UploadRequest(File file, UploadMultipartBoundary boundary)
    //{
    //    var urlData =
    //        new YadGetResourceUploadUrlRequest(HttpSettings, Authenticator, file.FullPath, file.OriginalSize)
    //        .MakeRequestAsync(_connectionLimiter)
    //        .Result;
    //    var url = urlData.Models[0].Data.UploadUrl;

    //    var result = new YadUploadRequest(HttpSettings, Authenticator, url, file.OriginalSize);
    //    return result;
    //}

    public ICloudHasher GetHasher()
    {
        return new YadHasher();
    }

    public bool SupportsAddSmallFileByHash => false;
    public bool SupportsDeduplicate => true;

    protected async Task<(HttpRequestMessage, string opId)> CreateUploadClientRequest(PushStreamContent content, File file)
    {
        var hash = (FileHashYad?)file.Hash;

        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                .Get(new YadModelV2GetResourceUploadUrl(file.FullPath,
                        size: file.OriginalSize, hashSha256: hash?.HashSha256.Value ?? string.Empty,
                        hashMd5: hash?.HashMd5.Value ?? string.Empty, force: true));

        CheckForError(itemInfo);

        var url = itemInfo?.UploadInfo?.Url;

        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(url),
            Method = HttpMethod.Put
        };

        request.Headers.Add("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("User-Agent", HttpSettings.UserAgent);

        /*
         * Пробуем разные варианты решения ошибки вида
         * Sent 3014656 request content bytes, but Content-Length promised 117811349.
         * в методе DoUpload в строке var responseMessage = await client.SendAsync(request);
         * Включаем Chunked и выключаем установки ContentLength, пусть framework сам считает.
         */
        request.Headers.TransferEncodingChunked = true;

        request.Content = content;
        //request.Content.Headers.ContentLength = file.OriginalSize;

        return (request, itemInfo?.UploadInfo?.Oid);
    }

    public virtual async Task<UploadFileResult> DoUpload(HttpClient client, PushStreamContent content, File file)
    {
        (var request, string opId) = await CreateUploadClientRequest(content, file);
        var responseMessage = await client.SendAsync(request);
        var res = responseMessage.ToUploadPathResult();

        res.NeedToAddFile = false;

        if (!string.IsNullOrEmpty(opId))
            WaitForOperation(opId);

        return res;
    }

    protected const string YadMediaPath = "/Media.wdyad";

    public virtual async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
    {
        if (path.IsLink)
            throw new NotImplementedException(nameof(FolderInfo));

        if (path.Path.StartsWith(YadMediaPath))
            return await MediaFolderInfo(path.Path);

        Logger.Debug($"Listing path {path.Path}");

        YadModelV2ResourceInfo fileOrFolderInfo = null;

        if (path.Path != "/")
        {
            Retry.Do(
                () => TimeSpan.Zero,
                () =>
                {
                    var fileTask = new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                       .Get(new YadModelV2ResourceInfo(path.Path));

                    fileOrFolderInfo = fileTask.Result;
                    return fileOrFolderInfo;
                },
                _ => false,
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryTimeout
                );

            if (fileOrFolderInfo is null)
                throw new IOException("Error reading file or directory information from server");

            // Для кода "HTTP_404" надо возвращать null
            CheckForError(fileOrFolderInfo);

            // Ни файла, ни папки с таким адресом нет
            if (fileOrFolderInfo.Result is null)
                return null;

            // it's a file
            if (fileOrFolderInfo.Result?.Type == "file")
                return fileOrFolderInfo.Result.ToFile(PublicBaseUrlDefault);
        }

        YadModelV2FolderInfo folderInfo = null;

        Retry.Do(
            () => TimeSpan.Zero,
            () =>
            {
                var folderTask = new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                     .Get(new YadModelV2FolderInfo(path.Path) { WithParent = true });

                folderInfo = folderTask.Result;
                return folderInfo;
            },
            _ => false,
            TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryTimeout
            );

        if (folderInfo is null)
            throw new IOException("Error reading file or directory information from server");

        // Для кода "HTTP_404" надо возвращать null
        CheckForError(folderInfo);

        Folder folder = folderInfo.FolderInfo.ToFolder(fileOrFolderInfo?.Result, path.Path, PublicBaseUrlDefault, null);
        folder.IsChildrenLoaded = true;
        return folder;
    }


    protected async Task<IEntry> MediaFolderInfo(string path)
    {
        var entry = await MediaFolderRootInfo();

        if (entry == null || entry is not Folder root)
            return null;

        if (WebDavPath.PathEquals(path, YadMediaPath))
            return root;

        string albumName = WebDavPath.Name(path);
        var child = entry.Descendants.FirstOrDefault(child => child.Name == albumName);
        if (child is null)
            return null;

        var album = child;

        var key = album.PublicLinks.Values.FirstOrDefault()?.Key;
        if (key == null)
            return null;

        var folderInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                   .Get(new YadModelV2FolderInfo(key, "/album"));

        CheckForError(folderInfo);

        Folder folder = folderInfo.FolderInfo.ToFolder(null, path, PublicBaseUrlDefault, null);
        folder.IsChildrenLoaded = true;

        return folder;
    }

    private async Task<IEntry> MediaFolderRootInfo()
    {
        Folder res = new Folder(YadMediaPath);

        _ = await new YadCommonRequest(HttpSettings, YadAuth)
            .With(new YadGetAlbumsSlicesPostModel(),
                out YadResponseModel<YadGetAlbumsSlicesRequestData, YadGetAlbumsSlicesRequestParams> slices)
            .With(new YadAlbumsPostModel(),
                out YadResponseModel<YadAlbumsRequestData[], YadAlbumsRequestParams> albums)
            .MakeRequestAsync(_connectionLimiter);

        var children = new List<IEntry>();

        if (slices.Data.Albums.Camera != null)
        {
            Folder folder =
                new Folder($"{YadMediaPath}/.{slices.Data.Albums.Camera.Id}")
                {
                    ServerFilesCount = (int)slices.Data.Albums.Camera.Count
                };
            children.Add(folder);
        }
        if (slices.Data.Albums.Photounlim != null)
        {
            Folder folder =
                new Folder($"{YadMediaPath}/.{slices.Data.Albums.Photounlim.Id}")
                {
                    ServerFilesCount = (int)slices.Data.Albums.Photounlim.Count
                };
            children.Add(folder);
        }
        if (slices.Data.Albums.Videos != null)
        {
            Folder folder =
                new Folder($"{YadMediaPath}/.{slices.Data.Albums.Videos.Id}")
                {
                    ServerFilesCount = (int)slices.Data.Albums.Videos.Count
                };
            children.Add(folder);
        }
        res.Descendants = res.Descendants.AddRange(children);

        foreach (var item in albums.Data)
        {
            Folder folder = new Folder($"{YadMediaPath}/{item.Title}");
            folder.PublicLinks.TryAdd(
                item.Public.PublicUrl,
                new PublicLinkInfo(item.Public.PublicUrl) { Key = item.Public.PublicKey });
        }

        return res;
    }


    public Task<FolderInfoResult> ItemInfo(RemotePath path, int offset = 0, int limit = int.MaxValue)
    {
        throw new NotImplementedException();
    }


    public async Task<AccountInfoResult> AccountInfo()
    {
        var result = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                               .Get(new YadModelV2AccountInfo());

        var res = result.ToAccountInfo();

        return res;
    }

    public async Task<CreateFolderResult> CreateFolder(string path)
    {
        //var res = await new YadCreateFolderRequest(HttpSettings, Authenticator, path)
        //    .MakeRequestAsync(_connectionLimiter);

        /* API changed in October 2024
        await new YaDCommonRequest(HttpSettings, Auth)
            .With(new YadCreateFolderPostModel(path),
                out YadResponseModel<YadCreateFolderRequestData, YadCreateFolderRequestParams> itemInfo)
            .MakeRequestAsync(_connectionLimiter);
        */
        _ = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                      .Get(new YadModelV2CreateFolder(path));
        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                      .Get(new YadModelV2ResourceInfo(path));

        var res = itemInfo.ToCreateFolderResult();
        return res;
    }

    public async Task<AddFileResult> AddFile(string fileFullPath, IFileHash fileHash, FileSize fileSize, DateTime dateTime,
        ConflictResolver? conflictResolver)
    {
        var hash = (FileHashYad?)fileHash;

        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                       .Get(new YadModelV2GetResourceUploadUrl(fileFullPath, fileSize, hash?.HashSha256.Value, hash?.HashMd5.Value));

        var res = new AddFileResult
        {
            Path = fileFullPath,
            Success = itemInfo.Errors.Count == 0
        };

        return await Task.FromResult(res);
    }

    public Task<CloneItemResult> CloneItem(string fromUrl, string toPath)
        => throw new NotImplementedException();

    public async Task<CopyResult> Copy(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
    {
        string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                 .Get(new YadModelV2Copy(sourceFullPath, destFullPath));

        var res = itemInfo.ToCopyResult();

        OnCopyCompleted(res, itemInfo?.OperationId);

        return res;
    }

    protected virtual void OnCopyCompleted(CopyResult res, string operationOpId)
        => WaitForOperation(operationOpId);

    public async Task<CopyResult> Move(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
    {
        string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                 .Get(new YadModelV2Move(sourceFullPath, destFullPath));

        var res = itemInfo.ToMoveResult();

        OnMoveCompleted(res, itemInfo?.OperationId);

        return res;
    }

    protected virtual void OnMoveCompleted(CopyResult res, string operationOpId)
        => WaitForOperation(operationOpId);

    public async Task<PublishResult> Publish(string fullPath)
    {
        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                 .Get(new YadModelV2Publish(fullPath));

        var res = itemInfo.ToPublishResult();

        if (res.IsSuccess)
            CachedSharedList.Value[fullPath] = [new(res.Url)];

        return res;
    }

    public async Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath)
    {
        foreach (var item in CachedSharedList.Value
            .Where(kvp => kvp.Key == fullPath).ToList())
        {
            CachedSharedList.Value.Remove(item.Key);
        }

        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                 .Get(new YadModelV2Unpublish(fullPath));

        var res = itemInfo.ToUnpublishResult();

        return res;
    }

    public async Task<DeleteResult> Remove(string fullPath)
    {
        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                 .Get(new YadModelV2Delete(fullPath));

        CheckForError(itemInfo);

        var res = itemInfo.ToRemoveResult();

        OnRemoveCompleted(res, itemInfo.OperationId);

        return res;
    }

    protected virtual void OnRemoveCompleted(DeleteResult res, string operationOpId) => WaitForOperation(operationOpId);

    public async Task<RenameResult> Rename(string fullPath, string newName)
    {
        string destPath = WebDavPath.Parent(fullPath);
        destPath = WebDavPath.Combine(destPath, newName);

        //var res = await new YadMoveRequest(HttpSettings, Authenticator, fullPath, destPath).MakeRequestAsync(_connectionLimiter);

        var itemInfo = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                                 .Get(new YadModelV2Move(fullPath, destPath));

        CheckForError(itemInfo);

        var res = itemInfo.ToRenameResult();

        OnRenameCompleted(res, itemInfo.OperationId);

        return res;
    }

    protected virtual void OnRenameCompleted(RenameResult res, string operationOpId) => WaitForOperation(operationOpId);

    public Dictionary<ShardType, ShardInfo> GetShardInfo1()
    {
        throw new NotImplementedException();
    }


    public IEnumerable<PublicLinkInfo> GetShareLinks(string path)
    {
        if (!CachedSharedList.Value.TryGetValue(path, out var links))
            yield break;

        foreach (var link in links)
            yield return link;
    }


    public async void CleanTrash()
    {
        var res = await new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                            .Get(new YadModelV2CleanTrash());

        CheckForError(res);

        WaitForOperation(res.CleanTrashInfo.Oid);
    }




    public IEnumerable<string> PublicBaseUrls { get; set; } = ["https://yadi.sk"];

    public string PublicBaseUrlDefault => PublicBaseUrls.FirstOrDefault();


    public string ConvertToVideoLink(Uri publicLink, SharedVideoResolution videoResolution)
    {
        throw new NotImplementedException("Yad not implemented ConvertToVideoLink");
    }

    protected virtual void WaitForOperation(string operationOpId)
    {
        if (string.IsNullOrWhiteSpace(operationOpId))
            return;

        var flagWatch = Stopwatch.StartNew();

        //YadResponseModel<YadOperationStatusData, YadOperationStatusParams> itemInfo = null;
        YadModelV2OperationStatus itemInfo = new YadModelV2OperationStatus(operationOpId);
        Retry.Do(
            () => TimeSpan.Zero,
            () => new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter).Get(itemInfo).Result,
            _ =>
            {
                /*
                 * Яндекс повторяет проверку при переносе папки каждый 9 секунд.
                 * Когда операция завершилась: "status": "DONE", "state": "COMPLETED", "type": "move"
                 *    "params": {
                 *         "source": "12-it's_uid-34:/disk/source-folder",
                 *         "target": "12-it's_uid-34:/disk/destination-folder"
                 *    },
                 * Когда операция еще в процессе: "status": "EXECUTING", "state": "EXECUTING", "type": "move"
                 *    "params": {
                 *         "source": "12-it's_uid-34:/disk/source-folder",
                 *         "target": "12-it's_uid-34:/disk/destination-folder"
                 *    },
                 */

                if (itemInfo is null)
                    throw new NullReferenceException("WaitForOperation itemInfo is null");

                if (itemInfo.Errors.Count > 0)
                {
                    Logger.Error($"WaitForOperation error: {CheckForError(itemInfo, throwException: false)}");
                    return true;
                }

                if (!itemInfo.OperationStatuses.TryGetValue(operationOpId, out var state))
                {
                    Logger.Error($"WaitForOperation failure: operation {operationOpId} is not registered");
                    return true;
                }

                var doAgain = state.State != "COMPLETED";

                //Logger.Debug($"WaitForOperation: doAgain={doAgain}, Oid={operationOpId}");

                if (doAgain)
                {
                    if (flagWatch.Elapsed > TimeSpan.FromSeconds(30))
                    {
                        Logger.Debug("Operation is still in progress, let's wait...");
                        flagWatch.Restart();
                    }
                }
                return doAgain;
            },
            TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryTimeout);
    }

    public virtual Task<CheckUpInfo> DetectOutsideChanges()
    {
        // Список все еще продолжающихся операций
        var operationsTask = new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                        .Get(new YadModelV2ActiveOperationsV2());

        // Надо учитывать, что счетчики обновляются через 10-15 секунд после операции
        var countersTask = new YadCommonRequestV2(HttpSettings, YadAuth, _connectionLimiter)
                        .Get(new YadModelV2JournalCountersV2());

        Task.WaitAll(operationsTask, countersTask);

        CheckForError(operationsTask.Result);
        CheckForError(countersTask.Result);

        var list = operationsTask.Result.ActiveOperations?
            .Select(x => new ActiveOperation
            {
                OpId = x.Id,
                Uid = x.Uid,
                Type = x.Type,
                SourcePath = DtoImportYadWeb.GetOpPath(x.Data.Source),
                TargetPath = DtoImportYadWeb.GetOpPath(x.Data.Target),
            })?.ToList();

        var info = new CheckUpInfo
        {
            AccountInfo = new CheckUpInfo.CheckInfo
            {
                //FilesCount = accountInfo?.Data?.FilesCount ?? 0,
                //Free = accountInfo?.Data?.Free ?? 0,
                //Trash = accountInfo?.Data?.Trash ?? 0,
                JournalCounters = new JournalCounters(countersTask.Result?.Result)
            },
            ActiveOperations = list,
        };

        return Task.FromResult(info);
    }
}
