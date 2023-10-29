﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests;
using YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.Types;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;
using YaR.Clouds.Common;

namespace YaR.Clouds.Base.Repos.MailRuCloud.Mobile
{
    /// <summary>
    /// Part of Mobile protocol.
    /// Not usable.
    /// </summary>
    class MobileRequestRepo : MailRuBaseRepo, IRequestRepo
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(MobileRequestRepo));

        private readonly SemaphoreSlim _connectionLimiter;

        public override HttpCommonSettings HttpSettings { get; } = new()
        {
            ClientId = "cloud-win",
            UserAgent = "CloudDiskOWindows 17.12.0009 beta WzBbt1Ygbm"
        };

        public MobileRequestRepo(CloudSettings settings, IWebProxy proxy, IAuth auth, int listDepth)
            : base(new Credentials(auth.Login, auth.Password))
        {
            _connectionLimiter = new SemaphoreSlim(settings.MaxConnectionCount);
            _listDepth = listDepth;

            HttpSettings.CloudSettings = settings;
            HttpSettings.Proxy = proxy;

            Authenticator = auth;

            _metaServer = new Cached<ServerRequestResult>(_ =>
                {
                    Logger.Debug("MetaServer expired, refreshing.");
                    var server = new MobMetaServerRequest(HttpSettings).MakeRequestAsync(_connectionLimiter).Result;
                    return server;
                },
                _ => TimeSpan.FromSeconds(MetaServerExpiresSec));

            ServicePointManager.DefaultConnectionLimit = int.MaxValue;

            // required for Windows 7 breaking connection
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12;

            //_downloadServer = new Cached<ServerRequestResult>(old =>
            //    {
            //        Logger.Debug("DownloadServer expired, refreshing.");
            //        var server = new GetServerRequest(HttpSettings).MakeRequestAsync(_connectionLimiter).Result;
            //        return server;
            //    },
            //    value => TimeSpan.FromSeconds(DownloadServerExpiresSec));
        }




        private readonly Cached<ServerRequestResult> _metaServer;
        private const int MetaServerExpiresSec = 20 * 60;

        //private readonly Cached<ServerRequestResult> _downloadServer;
		private readonly int _listDepth;
		//private const int DownloadServerExpiresSec = 20 * 60;


        

        //public HttpWebRequest UploadRequest(File file, UploadMultipartBoundary boundary)
        //{
        //    throw new NotImplementedException();
        //}

        public Stream GetDownloadStream(File file, long? start = null, long? end = null)
        {
            throw new NotImplementedException();
        }

        //public HttpWebRequest DownloadRequest(long instart, long inend, File file, ShardInfo shard)
        //{
        //    string url = $"{_downloadServer.Value.Url}{Uri.EscapeDataString(file.FullPath)}?token={Authenticator.AccessToken}&client_id={HttpSettings.ClientId}";

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
        //    //TODO: implement
        //    Logger.Warn($"{nameof(MobileRequestRepo)}.{nameof(BanShardInfo)} not implemented");
        //}

        public override Task<ShardInfo> GetShardInfo(ShardType shardType)
        {
            //TODO: must hide shard functionality into repo after DownloadStream and UploadStream refact

            var shi = new ShardInfo
            {
                Url = _metaServer.Value.Url,
                Type = shardType
            };

            return Task.FromResult(shi);
        }

        public Task<CloneItemResult> CloneItem(string fromUrl, string toPath)
        {
            throw new NotImplementedException();
        }

        public Task<CopyResult> Copy(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            throw new NotImplementedException();
        }

        public Task<CopyResult> Move(string sourceFullPath, string destinationPath, ConflictResolver? conflictResolver = null)
        {
            throw new NotImplementedException();
        }

        public async Task<IEntry> FolderInfo(RemotePath path, int offset = 0, int limit = int.MaxValue, int depth = 1)
        {
            if (path.IsLink)
                throw new NotImplementedException(nameof(FolderInfo));

            var req = new ListRequest(HttpSettings, Authenticator, _metaServer.Value.Url, path.Path, _listDepth);
            var res = await req.MakeRequestAsync(_connectionLimiter);

            switch (res.Item)
            {
                case FsFolder fsFolder:
                {
                    var folder = new Folder(fsFolder.Size == null ? 0 : (long)fsFolder.Size.Value, fsFolder.FullPath);
                    var children = new List<IEntry>();
                    foreach (var fsi in fsFolder.Items)
                    {
                        switch (fsi)
                        {
                            case FsFile fsfi:
                            {
                                var fi = new File(fsfi.FullPath, (long)fsfi.Size, new FileHashMrc(fsfi.Sha1))
                                {
                                    CreationTimeUtc = fsfi.ModifDate,
                                    LastWriteTimeUtc = fsfi.ModifDate
                                };
                                children.Add(fi);
                                break;
                            }
                            case FsFolder fsfo:
                            {
                                var fo = new Folder(fsfo.Size == null ? 0 : (long) fsfo.Size.Value, fsfo.FullPath);
                                children.Add(fo);
                                break;
                            }
                            default:
                                throw new Exception($"Unknown item type {fsi.GetType()}");
                        }
                    }
                    folder.Descendants = folder.Descendants.AddRange(children);
                    return folder;
                }
                case FsFile fsFile:
                {
                    var fi = new File(fsFile.FullPath, (long)fsFile.Size, new FileHashMrc(fsFile.Sha1))
                    {
                        CreationTimeUtc = fsFile.ModifDate,
                        LastWriteTimeUtc = fsFile.ModifDate
                    };

                    return fi;
                }
                default:
                    return null;
            }
        }

        public Task<FolderInfoResult> ItemInfo(RemotePath path, int offset = 0, int limit = int.MaxValue)
        {
            throw new NotImplementedException();
        }



        public async Task<AccountInfoResult> AccountInfo()
        {
            var req = await new AccountInfoRequest(HttpSettings, Authenticator).MakeRequestAsync(_connectionLimiter);
            var res = req.ToAccountInfo();
            return res;
        }

        public Task<PublishResult> Publish(string fullPath)
        {
            throw new NotImplementedException();
        }

        public Task<UnpublishResult> Unpublish(Uri publicLink, string fullPath = null)
        {
            throw new NotImplementedException();
        }

        public Task<RemoveResult> Remove(string fullPath)
        {
            throw new NotImplementedException();
        }

        public async Task<RenameResult> Rename(string fullPath, string newName)
        {
            string target = WebDavPath.Combine(WebDavPath.Parent(fullPath), newName);

            await new MoveRequest(HttpSettings, Authenticator, _metaServer.Value.Url, fullPath, target)
                .MakeRequestAsync(_connectionLimiter);
            var res = new RenameResult { IsSuccess = true };
            return res;
        }

        public Dictionary<ShardType, ShardInfo> GetShardInfo1()
        {
            throw new NotImplementedException("Mobile GetShardInfo1 not implemented");
        }

        public IEnumerable<PublicLinkInfo> GetShareLinks(string fullPath)
        {
            throw new NotImplementedException("Mobile GetShareLink not implemented");
        }

        public void CleanTrash()
        {
            throw new NotImplementedException();
        }

        public async Task<CreateFolderResult> CreateFolder(string path)
        {
            var folerRequest = await new CreateFolderRequest(HttpSettings, Authenticator, _metaServer.Value.Url, path)
                .MakeRequestAsync(_connectionLimiter);
            return folerRequest.ToCreateFolderResult();
        }

        public async Task<AddFileResult> AddFile(string fileFullPath, IFileHash fileHash, FileSize fileSize, DateTime dateTime, ConflictResolver? conflictResolver)
        {
            var res = await new MobAddFileRequest(HttpSettings, Authenticator, _metaServer.Value.Url,
                    fileFullPath, fileHash.Hash.Value, fileSize, dateTime, conflictResolver)
                .MakeRequestAsync(_connectionLimiter);

            return res.ToAddFileResult();
        }

        public async Task<CheckUpInfo> ActiveOperationsAsync() => await Task.FromResult<CheckUpInfo>(null);
    }
}
