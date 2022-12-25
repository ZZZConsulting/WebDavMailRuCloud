using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.Types;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models.Media;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Requests;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Base.Streams;
using YaR.Clouds.Common;
using Stream = System.IO.Stream;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb
{
    class YadWebRequestRepo : IRequestRepo
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(YadWebRequestRepo));

        private ItemOperation _lastRemoveOperation;
        
        private const int OperationStatusCheckIntervalMs = 300;
        private const int OperationStatusCheckRetryCount = 8;

        private readonly IBasicCredentials _creds;

        public YadWebRequestRepo(IWebProxy proxy, IBasicCredentials creds)
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;

            HttpSettings.Proxy = proxy;
            _creds = creds;
        }

        private async Task<Dictionary<string, IEnumerable<PublicLinkInfo>>> GetShareListInner()
        {
            throw new NotImplementedException();
            ////await new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
            ////    .With(new YadFolderRequestModel("/", "/published"),
            ////        out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo)
            ////    .MakeRequestAsync();

            ////var res = folderInfo.Data.Resources
            ////    .Where(it => !string.IsNullOrEmpty(it.Meta?.UrlShort))
            ////    .ToDictionary(
            ////        it => it.Path.Remove(0, "/disk".Length), 
            ////        it => Enumerable.Repeat(new PublicLinkInfo("short", it.Meta.UrlShort), 1));

            ////return res;
        }

        public IAuth Authent => CachedAuth.Value;

        private Cached<YadWebAuth> CachedAuth => _cachedAuth ??= new Cached<YadWebAuth>(_ => new YadWebAuth(HttpSettings, _creds), _ => TimeSpan.FromHours(23));
        private Cached<YadWebAuth> _cachedAuth;

        public Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> CachedSharedList => _cachedSharedList ??= new Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>>(_ =>
                    {
                        var res = GetShareListInner().Result;
                        return res;
                    }, 
                    _ => TimeSpan.FromSeconds(30));
        private Cached<Dictionary<string, IEnumerable<PublicLinkInfo>>> _cachedSharedList;


        public HttpCommonSettings HttpSettings { get; } = new()
        {
            UserAgent = ConstSettings.UserAgent,
            CloudDomain = ConstSettings.YandexCloudDomain,
            RequestContentType = ConstSettings.YandexDefaultRequestType
        };

        public Stream GetDownloadStream(File afile, long? start = null, long? end = null)
        {
            CustomDisposable<HttpWebResponse> ResponseGenerator(long instart, long inend, File file)
            {
                // Получить URL для скачивания файла
                var _ = new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
                    .With(new YadGetResourceRequestModel(file.FullPath),
                        out YadResponseModel itemInfo )
                    .MakeRequestAsync().Result;

                // Скачать файл
                HttpWebRequest request = new YadDownloadRequest(HttpSettings, (YadWebAuth)Authent, itemInfo.Href, instart, inend);
                var response = (HttpWebResponse)request.GetResponse();

                return new CustomDisposable<HttpWebResponse>
                {
                    Value = response,
                    OnDispose = () => {}
                };
            }

            var stream = new DownloadStream(ResponseGenerator, afile, start, end);
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

        private (HttpRequestMessage,string operationId) CreateUploadClientRequest(PushStreamContent content, File file)
        {
            //var hash = (FileHashYad?) file.Hash;
            var _ = new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadGetHrefForUploadRequestModel( file.FullPath ),
                    out YadGetResourceUploadResponse itemInfo )
                .MakeRequestAsync().Result;

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri( itemInfo.Href ),
                //1Method = HttpMethod.Post
                Method = itemInfo.Method
            };
            request.Headers.Add( "Authorization", $"OAuth {Authent.AccessToken}" );

            request.Headers.Add("Accept", "application/json" );
            //request.Headers.Add("Accept", "*/*");
            //request.Headers.TryAddWithoutValidation("User-Agent", HttpSettings.UserAgent);

            request.Content = content;
            //request.Content.Headers.ContentLength = file.OriginalSize;


            return (request,itemInfo.OperationId);
        }

        public async Task<UploadFileResult> DoUpload(HttpClient client, PushStreamContent content, File file)
        {
            (var request, string operationId) = CreateUploadClientRequest(content, file);
            //1var responseMessage = await client.SendAsync(request);
            Task<HttpResponseMessage> task = client.SendAsync( request );
            WaitForOperation( operationId );
            var responseMessage = await task;

            var ures = responseMessage.ToUploadPathResult();

            ures.NeedToAddFile = false;
            //await Task.Delay(1_000);;

            return ures;
        }

        private const string YadMediaPath = "/Media.wdyad";

        public async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
        {
            if (path.IsLink)
                throw new NotImplementedException(nameof(FolderInfo));

            if (path.Path.StartsWith(YadMediaPath))
                return await MediaFolderInfo(path.Path);

            // YaD perform async deletion
            //1YadResponseModel<YadItemInfoRequestData, YadItemInfoRequestParams> itemInfo = null;
            //1YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo = null;
            //1YadResponseModel<YadResourceStatsRequestData, YadResourceStatsRequestParams> resourceStats = null;

            YadDirEntryInfo folderInfo = null;

            bool hasRemoveOp = _lastRemoveOperation != null &&
                               WebDavPath.IsParentOrSame(path.Path, _lastRemoveOperation.Path) &&
                               (DateTime.Now - _lastRemoveOperation.DateTime).TotalMilliseconds < 1_000;
            try
            {
                Retry.Do(
                    () =>
                    {
                        var doPreSleep = hasRemoveOp ? TimeSpan.FromMilliseconds( OperationStatusCheckIntervalMs ) : TimeSpan.Zero;
                        if( doPreSleep > TimeSpan.Zero )
                            Logger.Debug( "Has remove op, sleep before" );
                        return doPreSleep;
                    },
                    //() => new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
                    //    .With(new YadItemInfoPostModel(path.Path), out itemInfo)
                    //    .With(new YadFolderInfoPostModel(path.Path), out folderInfo)
                    //    //1.With(new YadResourceStatsPostModel(path.Path), out resourceStats)
                    //    .MakeRequestAsync()
                    //    .Result,
                    () => new YadCommonRequest( HttpSettings, (YadWebAuth)Authent )
                        .With( new YadFolderRequestModel( path.Path ), out folderInfo )
                        .MakeRequestAsync()
                        .Result,
                    _ =>
                    {
                        var doAgain = hasRemoveOp &&
                               folderInfo.Content.Items.Any( file =>
                                   WebDavPath.PathEquals( file.Path.Remove( 0, "disk:".Length ), _lastRemoveOperation.Path ) );
                        if( doAgain )
                            Logger.Debug( "Remove op still not finished, let's try again" );
                        return doAgain;
                    },
                    TimeSpan.FromMilliseconds( OperationStatusCheckIntervalMs ), OperationStatusCheckRetryCount );
            }
            catch
            {
                if( folderInfo?.Error == "DiskNotFoundError" )
                    return null;
                throw;
            }

            switch(folderInfo?.Type)
            {
                case null:
                    return null;
                case "file":
                    return folderInfo.ToFile(PublicBaseUrlDefault);
                default:
                {
                    var entry = folderInfo.ToFolder(path.Path, PublicBaseUrlDefault);
                    var res = new Folder( 0, path.Path ) { IsChildsLoaded = true };
                    res.Files.AddRange( folderInfo.Content.Items
                        .Where( it => it.Type == "file" )
                        .Select( f => f.ToFile( PublicBaseUrlDefault ) )
                        .ToGroupedFiles()
                    );

                    foreach( var it in folderInfo.Content.Items.Where( it => it.Type == "dir" ) )
                    {
                        res.Folders.Add( it.ToFolder() );
                    }

                    return entry;
                }
            }
        }


        private async Task<IEntry> MediaFolderInfo(string path)
        {
            throw new NotImplementedException();

            ////if (await MediaFolderRootInfo() is not Folder root)
            ////    return null;
            
            ////if (WebDavPath.PathEquals(path, YadMediaPath))
            ////    return root;

            ////string albumName = WebDavPath.Name(path);
            ////var album = root.Folders.FirstOrDefault(f => f.Name == albumName);
            ////if (null == album)
            ////    return null;

            ////_ = new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
            ////    .With(new YadFolderRequestModel(album.PublicLinks.First().Key, "/album"),
            ////        out YadResponseModel<YadFolderInfoRequestData, YadFolderInfoRequestParams> folderInfo)
            ////    .MakeRequestAsync()
            ////    .Result;

            ////var entry = folderInfo.Data.ToFolder(null, null, path, PublicBaseUrlDefault);

            ////return entry;
        }

        private async Task<IEntry> MediaFolderRootInfo()
        {
            var res = new Folder(YadMediaPath);

            throw new NotImplementedException();
            ////_ = await new YadCommonRequest(HttpSettings, (YadWebAuth)Authent)
            ////    .With(new YadGetAlbumsSlicesPostModel(),
            ////        out YadResponseModel<YadGetAlbumsSlicesRequestData, YadGetAlbumsSlicesRequestParams> slices)
            ////    .With(new YadAlbumsPostModel(),
            ////        out YadResponseModel<YadAlbumsRequestData[], YadAlbumsRequestParams> albums)
            ////    .MakeRequestAsync();

            ////if (slices.Data.Albums.Camera != null)
            ////    res.Folders.Add(new Folder($"{YadMediaPath}/.{slices.Data.Albums.Camera.Id}")
            ////    { ServerFilesCount = (int)slices.Data.Albums.Camera.Count });
            ////if (slices.Data.Albums.Photounlim != null)
            ////    res.Folders.Add(new Folder($"{YadMediaPath}/.{slices.Data.Albums.Photounlim.Id}")
            ////    { ServerFilesCount = (int)slices.Data.Albums.Photounlim.Count });
            ////if (slices.Data.Albums.Videos != null)
            ////    res.Folders.Add(new Folder($"{YadMediaPath}/.{slices.Data.Albums.Videos.Id}")
            ////    { ServerFilesCount = (int)slices.Data.Albums.Videos.Count });

            ////res.Folders.AddRange(albums.Data.Select(al => new Folder($"{YadMediaPath}/{al.Title}")
            ////{
            ////    PublicLinks = { new PublicLinkInfo(al.Public.PublicUrl) {Key = al.Public.PublicKey} }
            ////}));

            return res;
        }


        public Task<FolderInfoResult> ItemInfo(RemotePath path, int offset = 0, int limit = int.MaxValue)
        {
            throw new NotImplementedException();
        }


        public async Task<AccountInfoResult> AccountInfo()
        {
            //var req = await new YadAccountInfoRequest(HttpSettings, (YadWebAuth)Authent).MakeRequestAsync();

            //1await new YaDCommonRequest(HttpSettings, (YadWebAuth) Authent)
            //1    .With(new YadAccountInfoPostModel(),
            //1        out YadResponseModel<YadAccountInfoRequestData, YadAccountInfoRequestParams> itemInfo)
            //1    .MakeRequestAsync();

            var diskInfo = await new YadDiskInfoRequest( HttpSettings, (YadWebAuth)Authent )
                .MakeRequestAsync();
            if( diskInfo.HasError )
                throw new AuthenticationException( $"{nameof( YadDiskInfoRequest )} error" );

            var res = new AccountInfoResult
            {
                FileSizeLimit = diskInfo.IsPaid
                    ? diskInfo.PaidMaxFileSize
                    : diskInfo.MaxFileSize,

                DiskUsage = new DiskUsage
                {
                    Total = diskInfo.TotalSpace,
                    Used = diskInfo.UsedSpace,
                    OverQuota = diskInfo.UsedSpace > diskInfo.TotalSpace
                }
            };

            //1var res = itemInfo.ToAccountInfo();
            return res;
        }

        public async Task<CreateFolderResult> CreateFolder(string path)
        {
            //var req = await new YadCreateFolderRequest(HttpSettings, (YadWebAuth)Authent, path)
            //    .MakeRequestAsync();

            await new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
                .With(new YadCreateFolderRequestModel(path), out YadResponseModel itemInfo)
                .MakeRequestAsync();

            var res = new CreateFolderResult
            {
                IsSuccess = itemInfo.Href != null && itemInfo.Error == null,
                Path = itemInfo.Href
            };

            return res;
        }

        public async Task<AddFileResult> AddFile(string fileFullPath, IFileHash fileHash, FileSize fileSize, DateTime dateTime,
            ConflictResolver? conflictResolver)
        {
            throw new NotImplementedException();
            ////var hash = (FileHashYad?)fileHash;

            ////var _ = new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
            ////    .With(new YadGetResourceUploadUrlPostModel(fileFullPath, fileSize, hash?.HashSha256.Value, hash?.HashMd5.Value),
            ////        out YadResponseModel<ResourceUploadUrlData, ResourceUploadUrlParams> itemInfo)
            ////    .MakeRequestAsync().Result;

            ////var res = new AddFileResult
            ////{
            ////    Path = fileFullPath,
            ////    Success = itemInfo.Data.Status == "hardlinked"
            ////};

            ////return await Task.FromResult(res);
        }

        public Task<CloneItemResult> CloneItem(string fromUrl, string toPath)
        {
            throw new NotImplementedException();
        }

        public async Task<CopyResult> Copy(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            throw new NotImplementedException();
            ////string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

            //////var req = await new YadCopyRequest(HttpSettings, (YadWebAuth)Authent, sourceFullPath, destFullPath)
            //////    .MakeRequestAsync();

            ////await new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
            ////    .With(new YadCopyPostModel(sourceFullPath, destFullPath),
            ////        out YadResponseModel<YadCopyRequestData, YadCopyRequestParams> itemInfo)
            ////    .MakeRequestAsync();

            ////var res = itemInfo.ToCopyResult();
            ////return res;
        }

        public async Task<CopyResult> Move(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            throw new NotImplementedException();
            ////string destFullPath = WebDavPath.Combine(destinationPath, WebDavPath.Name(sourceFullPath));

            ////await new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
            ////    .With(new YadMovePostModel(sourceFullPath, destFullPath), out YadResponseModel<YadMoveRequestData, YadMoveRequestParams> itemInfo)
            ////    .MakeRequestAsync();

            ////var res = itemInfo.ToMoveResult();

            ////WaitForOperation(itemInfo.Data.Oid);

            ////return res;
        }


        private void WaitForOperation(string operationOid)
        {
            YadStatusModel itemInfo = null;
            Retry.Do(
                () => TimeSpan.Zero,
                () => new YadCommonRequest( HttpSettings, (YadWebAuth)Authent )
                    .With( new YadOperationStatusRequestModel( operationOid ), out itemInfo )
                    .MakeRequestAsync()
                    .Result,
                _ =>
                {
                    var doAgain = null == itemInfo.Error && itemInfo.Status == "in-progress";
                    //if (doAgain)
                    //    Logger.Debug("Move op still not finished, let's try again");
                    return doAgain;
                },
                TimeSpan.FromMilliseconds( OperationStatusCheckIntervalMs ), int.MaxValue );
        }

        public async Task<PublishResult> Publish(string fullPath)
        {
            throw new NotImplementedException();
            ////await new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
            ////    .With(new YadPublishPostModel(fullPath, false), out YadResponseModel<YadPublishRequestData, YadPublishRequestParams> itemInfo)
            ////    .MakeRequestAsync();

            ////var res = itemInfo.ToPublishResult();

            ////if (res.IsSuccess)
            ////    CachedSharedList.Value[fullPath] = new List<PublicLinkInfo> {new(res.Url)};

            ////return res;
        }

        public async Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath)
        {
            throw new NotImplementedException();
            ////foreach( var item in CachedSharedList.Value
            ////    .Where(kvp => kvp.Key == fullPath).ToList())
            ////{
            ////    CachedSharedList.Value.Remove(item.Key);
            ////}

            ////await new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
            ////    .With(new YadPublishPostModel(fullPath, true), out YadResponseModel<YadPublishRequestData, YadPublishRequestParams> itemInfo)
            ////    .MakeRequestAsync();

            ////var res = itemInfo.ToUnpublishResult();

            ////return res;
        }

        public async Task<RemoveResult> Remove(string fullPath)
        {
            //var req = await new YadDeleteRequest(HttpSettings, (YadWebAuth)Authent, fullPath)
            //    .MakeRequestAsync();

            await new YadCommonRequest( HttpSettings, (YadWebAuth)Authent )
                .With( new YadDeleteRequestModel( fullPath ), out YadResponseModel itemInfo )
                .MakeRequestAsync();

            var res = new RemoveResult
            {
                IsSuccess = true,
                DateTime = DateTime.Now,
                Path = fullPath
            };

            if( res.IsSuccess )
                _lastRemoveOperation = res.ToItemOperation();

            return res;
        }

        public async Task<RenameResult> Rename(string fullPath, string newName)
        {
            string destPath = WebDavPath.Parent( fullPath );
            destPath = WebDavPath.Combine( destPath, newName );

            //var req = await new YadMoveRequest(HttpSettings, (YadWebAuth)Authent, fullPath, destPath).MakeRequestAsync();

            await new YadCommonRequest( HttpSettings, (YadWebAuth)Authent )
                .With( new YadMoveRequestModel( fullPath, destPath ), out YadResponseModel itemInfo )
                .MakeRequestAsync();

            var res = new RenameResult
            {
                IsSuccess = itemInfo.Error == null
            };

            ////if( res.IsSuccess )
            ////    WaitForOperation( itemInfo.Data.Oid );

            if( res.IsSuccess )
            {
                _lastRemoveOperation = new ItemOperation()
                {
                    DateTime = DateTime.Now,
                    Path = fullPath
                };
            }

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
            throw new NotImplementedException();
            ////await new YadCommonRequest(HttpSettings, (YadWebAuth) Authent)
            ////    .With(new YadCleanTrashPostModel(), 
            ////        out YadResponseModel<YadCleanTrashData, YadCleanTrashParams> _)
            ////    .MakeRequestAsync();
        }


        

        public IEnumerable<string> PublicBaseUrls { get; set; } = new[]
        {
            "https://yadi.sk"
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
