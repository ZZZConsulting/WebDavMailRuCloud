using System;
using System.IO;
using System.Linq;
using YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb
{
    static class DtoImportYadWeb
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(DtoImportYadWeb));

        //public static AccountInfoResult ToAccountInfo(this YadResponseModel<YadAccountInfoRequestData, YadAccountInfoRequestParams> data)
        //{
        //    var info = data.Data;
        //    var res = new AccountInfoResult
        //    {

        //        FileSizeLimit = info.FilesizeLimit,

        //        DiskUsage = new DiskUsage
        //        {
        //            Total = info.Limit,
        //            Used = info.Used,
        //            OverQuota = info.Used > info.Limit
        //        }
        //    };

        //    return res;
        //}


        public static IEntry ToFolder( this YadDirEntryInfo response, string path, string publicBaseUrl )
        {
            var fi = response.Content.Items;

            var res = new Folder( 0, path ) { IsChildsLoaded = true };
            if( !string.IsNullOrEmpty( response.PublickUrl ) )
                res.PublicLinks.Add( new PublicLinkInfo( "short", response.PublickUrl ) );

            res.Files.AddRange( fi
                .Where( it => it.Type == "file" )
                .Select( f => f.ToFile( publicBaseUrl ) )
                .ToGroupedFiles()
            );

            foreach( var it in fi.Where( it => it.Type == "dir" ) )
            {
                res.Folders.Add( it.ToFolder() );
            }

            return res;
        }

        public static File ToFile(this YadDirEntryInfo dirEntry, string publicBaseUrl )
        {
            var path = dirEntry.Path.Remove( 0, "disk:".Length );

            var res = new File(path, dirEntry.Size ?? throw new Exception("The size of the file is empty!") )
            {
                //1CreationTimeUtc = UnixTimeStampToDateTime(data.Ctime, DateTime.MinValue),
                //1LastAccessTimeUtc = UnixTimeStampToDateTime(data.Utime, DateTime.MinValue),
                //1LastWriteTimeUtc = UnixTimeStampToDateTime(data.Mtime, DateTime.MinValue)
                CreationTimeUtc = dirEntry.Created?.ToUniversalTime() ?? DateTime.MinValue,
                LastAccessTimeUtc = dirEntry.Modified?.ToUniversalTime() ?? DateTime.MinValue,
                LastWriteTimeUtc = dirEntry.Modified?.ToUniversalTime() ?? DateTime.MinValue,
                //PublicLink = data.Meta.UrlShort.StartsWith(publicBaseUrl)
                //    ? data.Meta.UrlShort.Remove(0, publicBaseUrl.Length)
                //    : data.Meta.UrlShort
            };
            if (!string.IsNullOrEmpty(dirEntry.PublickUrl))
                res.PublicLinks.Add(new PublicLinkInfo("short", dirEntry.PublickUrl) );
            
            return res;
        }

        public static Folder ToFolder( this YadDirEntryInfo dirEntry )
        {
            var path = dirEntry.Path.Remove( 0, "disk:".Length );

            var res = new Folder( path )
            {
                IsChildsLoaded = false,
                CreationTimeUtc = dirEntry.Created?.ToUniversalTime() ?? DateTime.MinValue,
                LastWriteTimeUtc = dirEntry.Modified?.ToUniversalTime() ?? DateTime.MinValue,
                LastAccessTimeUtc = dirEntry.Modified?.ToUniversalTime() ?? DateTime.MinValue
            };

            return res;
        }

        ////public static RenameResult ToRenameResult(this YadResponseModel<YadMoveRequestData, YadMoveRequestParams> data)
        ////{
        ////    var res = new RenameResult
        ////    {
        ////        IsSuccess = null == data.Data.Error ,
        ////        DateTime = DateTime.Now,
        ////        Path = data.Params.Src.Remove(0, "/disk".Length)
        ////    };
        ////    return res;
        ////}

        //public static ItemOperation ToItemOperation(this RenameResult data)
        //{
        //    var res = new ItemOperation
        //    {
        //        DateTime = data.DateTime,
        //        Path = data.Path
        //    };
        //    return res;
        //}

        ////public static RemoveResult ToRemoveResult(this YadResponseModel<YadDeleteRequestData, YadDeleteRequestParams> data)
        ////{
        ////    var res = new RemoveResult
        ////    {
        ////        IsSuccess = true,
        ////        DateTime = DateTime.Now,
        ////        Path = data.Params.Id.Remove(0, "/disk".Length)
        ////    };
        ////    return res;
        ////}

        public static ItemOperation ToItemOperation(this RemoveResult data)
        {
            var res = new ItemOperation
            {
                DateTime = data.DateTime,
                Path = data.Path
            };
            return res;
        }

        //public static CreateFolderResult ToCreateFolderResult(this YadCreateFolderRequestParams data)
        //{
        //    var res = new CreateFolderResult
        //    {
        //        IsSuccess = true,
        //        Path = data.Id.Remove(0, "/disk".Length)
        //    };
        //    return res;
        //}

        ////public static CopyResult ToCopyResult(this YadResponseModel<YadCopyRequestData, YadCopyRequestParams> data)
        ////{
        ////    var res = new CopyResult
        ////    {
        ////        IsSuccess = true,
        ////        NewName = data.Params.Dst.Remove(0, "/disk".Length),
        ////        OldFullPath = data.Params.Src.Remove(0, "/disk".Length),
        ////        DateTime = DateTime.Now
        ////    };
        ////    return res;
        ////}

        ////public static CopyResult ToMoveResult(this YadResponseModel<YadMoveRequestData, YadMoveRequestParams> data)
        ////{
        ////    var res = new CopyResult
        ////    {
        ////        IsSuccess = null == data.Data.Error,
        ////        NewName = data.Params.Dst.Remove(0, "/disk".Length),
        ////        OldFullPath = data.Params.Src.Remove(0, "/disk".Length),
        ////        DateTime = DateTime.Now
        ////    };
        ////    return res;
        ////}

        //public static ItemOperation ToItemOperation(this CopyResult data)
        //{
        //    var res = new ItemOperation
        //    {
        //        DateTime = data.DateTime,
        //        Path = data.OldFullPath
        //    };
        //    return res;
        //}

        ////public static PublishResult ToPublishResult(this YadResponseModel<YadPublishRequestData, YadPublishRequestParams> data)
        ////{
        ////    var res = new PublishResult
        ////    {
        ////        IsSuccess = !string.IsNullOrEmpty(data.Data.ShortUrl),
        ////        Url = data.Data.ShortUrl
        ////    };
        ////    return res;
        ////}

        ////public static UnpublishResult ToUnpublishResult(this YadResponseModel<YadPublishRequestData, YadPublishRequestParams> data)
        ////{
        ////    var res = new UnpublishResult
        ////    {
        ////        IsSuccess = true
        ////    };
        ////    return res;
        ////}



        private static DateTime UnixTimeStampToDateTime(double unixTimeStamp, DateTime defaultvalue)
        {
            try
            {
                // Unix timestamp is seconds past epoch
                var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                dtDateTime = dtDateTime.AddSeconds(unixTimeStamp); //.ToLocalTime(); - doesn't need, clients usially convert to localtime by itself
                return dtDateTime;
            }
            catch (Exception e)
            {
                Logger.Error($"Error converting unixTimeStamp {unixTimeStamp} to DateTime, {e.Message}");
                return defaultvalue;
            }
        }
    }
}
