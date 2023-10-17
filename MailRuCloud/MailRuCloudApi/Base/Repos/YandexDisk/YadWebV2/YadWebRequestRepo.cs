﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Repos.YandexDisk.YadWebV2.Models;
using YaR.Clouds.Base.Repos.YandexDisk.YadWebV2.Models.Media;
using YaR.Clouds.Base.Repos.YandexDisk.YadWebV2.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;
using Stream = System.IO.Stream;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWebV2
{
    class YadWebRequestRepo : IRequestRepo
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadWebRequestRepo));

        private ItemOperation _lastRemoveOperation;
        
        private const int OperationStatusCheckIntervalMs = 300;
        private const int OperationStatusCheckRetryCount = 8;

        private readonly IBasicCredentials _creds;

        public YadWebRequestRepo(CloudSettings settings, IWebProxy proxy, IBasicCredentials creds)
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;

            HttpSettings = new()
            {
                UserAgent = settings.UserAgent,
                CloudSettings = settings,
            };

            HttpSettings.Proxy = proxy;
            _creds = creds;
        }

        private async Task<Dictionary<string, IEnumerable<PublicLinkInfo>>> GetShareListInner()
        {
            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadFolderInfoPostModel("/", "/published"),
                    out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo)
                .MakeRequestAsync();

            var res = folderInfo.Data.Resources
                .Where(it => !string.IsNullOrEmpty(it.Meta?.UrlShort))
                .ToDictionary(
                    it => it.Path.Remove(0, "/disk".Length), 
                    it => Enumerable.Repeat(new PublicLinkInfo("short", it.Meta.UrlShort), 1));

            return res;
        }

        public IAuth Authent => CachedAuth.Value;

        private Cached<YadWebAuth> CachedAuth => _cachedAuth ??=
                new Cached<YadWebAuth>(_ => new YadWebAuth(HttpSettings, _creds), _ => TimeSpan.FromHours(23));
        private Cached<YadWebAuth> _cachedAuth;

        public Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> CachedSharedList
            => _cachedSharedList ??= new Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>>(
                _ =>
                    {
                        var res = GetShareListInner().Result;
                        return res;
                    }, 
                    _ => TimeSpan.FromSeconds(30));
        private Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> _cachedSharedList;


        public HttpCommonSettings HttpSettings { get; private set; }

        public Stream GetDownloadStream(File aFile, long? start = null, long? end = null)
        {
            CustomDisposable<HttpWebResponse> ResponseGenerator(long instart, long inend, File file)
            {
                //var urldata = new YadGetResourceUrlRequest(HttpSettings, (YadWebAuth)Authent, file.FullPath)
                //    .MakeRequestAsync()
                //    .Result;
                string url = null;
                if (file.DownloadUrlCache == null)
                {
                    var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth)Authent)
                        .With(new YadGetResourceUrlPostModel(file.FullPath),
                            out YadResponseModel<ResourceUrlData, ResourceUrlParams> itemInfo)
                        .MakeRequestAsync().Result;

                    if (itemInfo == null ||
                        itemInfo.Error != null ||
                        itemInfo.Data == null ||
                        itemInfo.Data.Error != null ||
                        itemInfo?.Data?.File == null)
                    {
                        throw new FileNotFoundException(string.Concat(
                            "File reading error ", itemInfo?.Error?.Message,
                            " ",
                            itemInfo?.Data?.Error?.Message,
                            " ",
                            itemInfo?.Data?.Error?.Body?.Title));
                    }
                    url = "https:" + itemInfo.Data.File;
                    file.DownloadUrlCache = url;
                }
                else
                {
                    url = file.DownloadUrlCache;
                }
                HttpWebRequest request = new YadDownloadRequest(HttpSettings, (YadWebAuth)Authent, url, instart, inend);
                var response = (HttpWebResponse)request.GetResponse();

                return new CustomDisposable<HttpWebResponse>
                {
                    Value = response,
                    OnDispose = () => {}
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
        //    var urldata = 
        //        new YadGetResourceUploadUrlRequest(HttpSettings, (YadWebAuth)Authent, file.FullPath, file.OriginalSize)
        //        .MakeRequestAsync()
        //        .Result;
        //    var url = urldata.Models[0].Data.UploadUrl;

            //    var result = new YadUploadRequest(HttpSettings, (YadWebAuth)Authent, url, file.OriginalSize);
            //    return result;
            //}

        public ICloudHasher GetHasher()
        {
            return new YadHasher();
        }

        public bool SupportsAddSmallFileByHash => false;
        public bool SupportsDeduplicate => true;

        private HttpRequestMessage CreateUploadClientRequest(PushStreamContent content, File file)
        {
            var hash = (FileHashYad?) file.Hash;
            var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadGetResourceUploadUrlPostModel(file.FullPath, file.OriginalSize, 
                        hash?.HashSha256.Value ?? string.Empty, 
                        hash?.HashMd5.Value ?? string.Empty),
                    out YadResponseModel<ResourceUploadUrlData, ResourceUploadUrlParams> itemInfo)
                .MakeRequestAsync().Result;
            var url = itemInfo.Data.UploadUrl;

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Put
            };

            request.Headers.Add("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("User-Agent", HttpSettings.UserAgent);

            request.Content = content;
            request.Content.Headers.ContentLength = file.OriginalSize;


            return request;
        }

        public async Task<UploadFileResult> DoUpload(HttpClient client, PushStreamContent content, File file)
        {
            var request = CreateUploadClientRequest(content, file);
            var responseMessage = await client.SendAsync(request);
            var ures = responseMessage.ToUploadPathResult();

            ures.NeedToAddFile = false;
            //await Task.Delay(1_000);;

            return ures;
        }

        private const string YadMediaPath = "/Media.wdyad";

        private const int FirstReadEntriesCount = 200;

        public async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
        {
            if (path.IsLink)
                throw new NotImplementedException(nameof(FolderInfo));

            if (path.Path.StartsWith(YadMediaPath))
                return await MediaFolderInfo(path.Path);

            // YaD perform async deletion
            YadResponseModel<YadItemInfoRequestData, YadItemInfoRequestParams> entryInfo = null;
            YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo = null;
            YadResponseModel<YadResourceStatsRequestData, YadResourceStatsRequestParams> entryStats = null;

            bool hasRemoveOp = _lastRemoveOperation != null &&
                               WebDavPath.IsParentOrSame(path.Path, _lastRemoveOperation.Path) &&
                               (DateTime.Now - _lastRemoveOperation.DateTime).TotalMilliseconds < 1_000;
            Retry.Do(
                () =>
                {
                    var doPreSleep = hasRemoveOp ? TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs) : TimeSpan.Zero;
                    if (doPreSleep > TimeSpan.Zero)
                        Logger.Debug("Has remove op, sleep before");
                    return doPreSleep;
                },
                () => new YaDCommonRequest(HttpSettings, (YadWebAuth)Authent)
                    .With(new YadItemInfoPostModel(path.Path), out entryInfo)
                    .With(new YadFolderInfoPostModel(path.Path) { WithParent = true, Amount = FirstReadEntriesCount }, out folderInfo)
                    .With(new YadResourceStatsPostModel(path.Path), out entryStats)
                    .MakeRequestAsync()
                    .Result,
                _ =>
                {
                    bool doAgain = false;
                    if (hasRemoveOp && _lastRemoveOperation != null)
                    {
                        string cmpPath = WebDavPath.Combine("/disk", _lastRemoveOperation.Path);
                        doAgain = hasRemoveOp &&
                           folderInfo.Data.Resources.Any(r => WebDavPath.PathEquals(r.Path, cmpPath));
                    }
                    if (doAgain)
                        Logger.Debug("Remove op still not finished, let's try again");
                    return doAgain;
                },
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryCount);


            var entryData = entryInfo?.Data;
            if (entryData?.Type is null)
                return null;
            if (entryData.Type == "file")
                return entryData.ToFile();

            Folder folder = folderInfo.Data.ToFolder(entryData, entryStats.Data, path.Path);

            // Если количество полученных элементов списка меньше максимального запрошенного числа элементов,
            // даже с учетом, что в число элементов сверх запрошенного входит информация
            // о папке-контейнере (папке, чей список элементов запросили), то считаем,
            // что получен полный список содержимого папки и возвращает данные.
            if ((folderInfo.Data.Resources?.Count ?? int.MaxValue) < FirstReadEntriesCount)
                return folder;
            // В противном случае делаем несколько параллельных выборок для ускорения чтения списков с сервера.

            int entryCount = folderInfo?.Data?.Resources.FirstOrDefault()?.Meta?.TotalEntityCount ?? 1;
            int maxParallel = 1;
            int parallelCount = int.MaxValue;
            if (entryCount < 300 + FirstReadEntriesCount)
            {
                maxParallel = 1;
                parallelCount = int.MaxValue;
            }
            else
            if (entryCount >= 3000 + FirstReadEntriesCount)
            {
                maxParallel = 10;
                // Читать данные с сервера будем немного внахлест чтобы случайно не пропустить что-либо.
                // Дубликаты с одинаковыми путями самоустранятся при сливании в один словарь.
                parallelCount = entryCount / 10 + 5;
            }
            else
            {
                maxParallel = entryCount / 300 + 1;
                parallelCount = 304;
            }

            Retry.Do(
                () =>
                {
                    var doPreSleep = hasRemoveOp ? TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs) : TimeSpan.Zero;
                    if (doPreSleep > TimeSpan.Zero)
                        Logger.Debug("Has remove op, sleep before");
                    return doPreSleep;
                },
                () =>
                {
                    Parallel.For(0, maxParallel, (int index) =>
                    {
                        YadResponseResult noReturn = new YaDCommonRequest(HttpSettings, (YadWebAuth)Authent)
                            .With(new YadFolderInfoPostModel(path.Path)
                            {
                                Offset = FirstReadEntriesCount + parallelCount * index - 2 /* отступ для чтения внахлест */,
                                Amount = index == maxParallel - 1 ? int.MaxValue : parallelCount
                            }, out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderPartInfo)
                            .MakeRequestAsync()
                            .Result;

                        if (folderPartInfo?.Data is not null && folderPartInfo.Error is null)
                            folder.MergeData(folderPartInfo.Data, entryData.Path);
                    });
                    YadResponseResult retValue = null;
                    return retValue;
                },
                _ =>
                {
                    //TODO: Здесь полностью неправильная проверка
                    bool doAgain = false;
                    if (hasRemoveOp && _lastRemoveOperation != null)
                    {
                        string cmpPath = WebDavPath.Combine("/disk", _lastRemoveOperation.Path);
                        doAgain = hasRemoveOp &&
                           folderInfo.Data.Resources.Any(r => WebDavPath.PathEquals(r.Path, cmpPath));
                    }
                    if (doAgain)
                        Logger.Debug("Remove op still not finished, let's try again");
                    return doAgain;
                },
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryCount);

            return folder;
        }


        private async Task<IEntry> MediaFolderInfo(string path)
        {
            if (await MediaFolderRootInfo() is not Folder root)
                return null;
            
            if (WebDavPath.PathEquals(path, YadMediaPath))
                return root;

            //string albumName = WebDavPath.Name(path);
            //var album = root.Folders.Values.FirstOrDefault(f => f.Name == albumName);
            //if (null == album)
            //    return null;
            // Вариант без перебора предпочтительнее
            if (!root.Folders.TryGetValue(path, out var album))
                return null;

            var key = album.PublicLinks.Values.FirstOrDefault()?.Key;
            if (key == null)
                return null;

            _ = new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadFolderInfoPostModel(key, "/album"),
                    out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo)
                .MakeRequestAsync()
                .Result;

            Folder folder = folderInfo.Data.ToFolder(null, null, path);
            folder.MergeData(folderInfo.Data, path);

            return folder;
        }

        private async Task<IEntry> MediaFolderRootInfo()
        {
            var res = new Folder(YadMediaPath);

            _ = await new YaDCommonRequest(HttpSettings, (YadWebAuth)Authent)
                .With(new YadGetAlbumsSlicesPostModel(),
                    out YadResponseModel<YadGetAlbumsSlicesRequestData, YadGetAlbumsSlicesRequestParams> slices)
                .With(new YadAlbumsPostModel(),
                    out YadResponseModel<YadAlbumsRequestData[], YadAlbumsRequestParams> albums)
                .MakeRequestAsync();

            if (slices.Data.Albums.Camera != null)
            {
                Folder folder = new Folder($"{YadMediaPath}/.{slices.Data.Albums.Camera.Id}")
                { ServerFilesCount = (int)slices.Data.Albums.Camera.Count };
                res.Folders.TryAdd(folder.FullPath, folder);
            }
            if (slices.Data.Albums.Photounlim != null)
            {
                Folder folder = new Folder($"{YadMediaPath}/.{slices.Data.Albums.Photounlim.Id}")
                { ServerFilesCount = (int)slices.Data.Albums.Photounlim.Count };
                res.Folders.TryAdd(folder.FullPath, folder);
            }
            if (slices.Data.Albums.Videos != null)
            {
                Folder folder = new Folder($"{YadMediaPath}/.{slices.Data.Albums.Videos.Id}")
                { ServerFilesCount = (int)slices.Data.Albums.Videos.Count };
                res.Folders.TryAdd(folder.FullPath, folder);
            }

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
            //var req = await new YadAccountInfoRequest(HttpSettings, (YadWebAuth)Authent).MakeRequestAsync();

            await new YaDCommonRequest(HttpSettings, (YadWebAuth)Authent)
                .With(new YadAccountInfoPostModel(),
                    out YadResponseModel<YadAccountInfoRequestData, YadAccountInfoRequestParams> itemInfo)
                .MakeRequestAsync();

            var res = itemInfo.ToAccountInfo();
            return res;
        }

        public async Task<CreateFolderResult> CreateFolder(string path)
        {
            //var req = await new YadCreateFolderRequest(HttpSettings, (YadWebAuth)Authent, path)
            //    .MakeRequestAsync();

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadCreateFolderPostModel(path),
                    out YadResponseModel<YadCreateFolderRequestData, YadCreateFolderRequestParams> itemInfo)
                .MakeRequestAsync();

            var res = itemInfo.Params.ToCreateFolderResult();
            return res;
        }

        public async Task<AddFileResult> AddFile(string fileFullPath, IFileHash fileHash, FileSize fileSize, DateTime dateTime,
            ConflictResolver? conflictResolver)
        {
            var hash = (FileHashYad?)fileHash;

            var _ = new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadGetResourceUploadUrlPostModel(fileFullPath, fileSize, hash?.HashSha256.Value, hash?.HashMd5.Value),
                    out YadResponseModel<ResourceUploadUrlData, ResourceUploadUrlParams> itemInfo)
                .MakeRequestAsync().Result;

            var res = new AddFileResult
            {
                Path = fileFullPath,
                Success = itemInfo.Data.Status == "hardlinked"
            };

            return await Task.FromResult(res);
        }

        public Task<CloneItemResult> CloneItem(string fromUrl, string toPath)
        {
            throw new NotImplementedException();
        }

        public async Task<CopyResult> Copy(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

            //var req = await new YadCopyRequest(HttpSettings, (YadWebAuth)Authent, sourceFullPath, destFullPath)
            //    .MakeRequestAsync();

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadCopyPostModel(sourceFullPath, destFullPath),
                    out YadResponseModel<YadCopyRequestData, YadCopyRequestParams> itemInfo)
                .MakeRequestAsync();

            var res = itemInfo.ToCopyResult();
            return res;
        }

        public async Task<CopyResult> Move(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadMovePostModel(sourceFullPath, destFullPath), out YadResponseModel<YadMoveRequestData, YadMoveRequestParams> itemInfo)
                .MakeRequestAsync();

            var res = itemInfo.ToMoveResult();

            WaitForOperation(itemInfo.Data.Oid);

            return res;
        }


        private void WaitForOperation(string operationOid)
        {
            YadResponseModel<YadOperationStatusData, YadOperationStatusParams> itemInfo = null;
            Retry.Do(
                () => TimeSpan.Zero,
                () => new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                    .With(new YadOperationStatusPostModel(operationOid), out itemInfo)
                    .MakeRequestAsync()
                    .Result,
                _ =>
                {
                    var doAgain = null == itemInfo.Data.Error && itemInfo.Data.State != "COMPLETED";
                    //if (doAgain)
                    //    Logger.Debug("Move op still not finished, let's try again");
                    return doAgain;
                }, 
                TimeSpan.FromMilliseconds(OperationStatusCheckIntervalMs), OperationStatusCheckRetryCount);
        }

        public async Task<PublishResult> Publish(string fullPath)
        {
            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadPublishPostModel(fullPath, false), out YadResponseModel<YadPublishRequestData, YadPublishRequestParams> itemInfo)
                .MakeRequestAsync();

            var res = itemInfo.ToPublishResult();

            if (res.IsSuccess)
                CachedSharedList.Value[fullPath] = new List<PublicLinkInfo> {new(res.Url)};

            return res;
        }

        public async Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath)
        {
            foreach (var item in CachedSharedList.Value
                .Where(kvp => kvp.Key == fullPath).ToList())
            {
                CachedSharedList.Value.Remove(item.Key);
            }

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadPublishPostModel(fullPath, true), out YadResponseModel<YadPublishRequestData, YadPublishRequestParams> itemInfo)
                .MakeRequestAsync();

            var res = itemInfo.ToUnpublishResult();

            return res;
        }

        public async Task<RemoveResult> Remove(string fullPath)
        {
            //var req = await new YadDeleteRequest(HttpSettings, (YadWebAuth)Authent, fullPath)
            //    .MakeRequestAsync();

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadDeletePostModel(fullPath),
                    out YadResponseModel<YadDeleteRequestData, YadDeleteRequestParams> itemInfo)
                .MakeRequestAsync();

            var res = itemInfo.ToRemoveResult();
                
            if (res.IsSuccess)
                _lastRemoveOperation = res.ToItemOperation();

            return res;
        }

        public async Task<RenameResult> Rename(string fullPath, string newName)
        {
            string destPath = WebDavPath.Parent(fullPath);
            destPath = WebDavPath.Combine(destPath, newName);

            //var req = await new YadMoveRequest(HttpSettings, (YadWebAuth)Authent, fullPath, destPath).MakeRequestAsync();

            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadMovePostModel(fullPath, destPath),
                    out YadResponseModel<YadMoveRequestData, YadMoveRequestParams> itemInfo)
                .MakeRequestAsync();

            var res = itemInfo.ToRenameResult();

            if (res.IsSuccess)
                WaitForOperation(itemInfo.Data.Oid);

            //if (res.IsSuccess)
            //    _lastRemoveOperation = res.ToItemOperation();


            return res;
        }

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
            await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadCleanTrashPostModel(), 
                    out YadResponseModel<YadCleanTrashData, YadCleanTrashParams> _)
                .MakeRequestAsync();
        }


        

        public IEnumerable<string> PublicBaseUrls { get; set; } = new[]
        {
            "https://disk.yandex.ru"
        };
        public string PublicBaseUrlDefault => PublicBaseUrls.First();







        public string ConvertToVideoLink(Uri publicLink, SharedVideoResolution videoResolution)
        {
            throw new NotImplementedException("Yad not implemented ConvertToVideoLink");
        }
    }

    //public static class Zzz
    //{
    //    private static Stopwatch _sw = new Stopwatch();

    //    static Zzz()
    //    {
    //        _sw.Start();
    //    }

    //    public static long ElapsedMs()
    //    {
    //        return _sw.ElapsedMilliseconds;
    //    }
    //}
}
