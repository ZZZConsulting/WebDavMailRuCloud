﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.Types;
using YaR.Clouds.Base.Requests;

namespace YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests
{
    internal class SharedFoldersListRequest : BaseRequestMobile<SharedFoldersListRequest.Result>
    {
        public SharedFoldersListRequest(HttpCommonSettings settings, IAuth auth, string metaServer) : base(settings, auth, metaServer)
        {
        }

        protected override byte[] CreateHttpContent()
        {
            using var stream = new RequestBodyStream();

            stream.WritePu16((byte)Operation.SharedFoldersList);
            //stream.WriteWithLength(new byte[0]);
            var body = stream.GetBytes();
            return body;
        }

        protected override RequestResponse<Result> DeserializeMessage(NameValueCollection responseHeaders, ResponseBodyStream data)
        {
            switch (data.OperationResult)
            {
                case OperationResult.Ok:
                    break;
                default:
                    throw new Exception($"{nameof(SharedFoldersListRequest)} failed with result code {data.OperationResult}");
            }


            var res = new Result
            {
                OperationResult = data.OperationResult,
                Container = new Dictionary<string, FsFolder>()
            };


            //var z = data.ReadAllBytes();
            //
            var opres = data.OperationResult; //data.ReadShort();
            switch (opres)
            {
                case 0:
                    long cnt = data.ReadPu32();
                    for (long j = 0; j < cnt; j++)
                    {
                        var treeId = data.ReadTreeId();
                        string fullPath = data.ReadString();
                        res.Container[fullPath] = new FsFolder(fullPath, treeId, CloudFolderType.Shared, null, 0);
                        data.ReadULong();
                    }
                    break;
                default:
                    throw new Exception($"{nameof(SharedFoldersListRequest)}: Unknown parse operation {opres}");
            }

            return new RequestResponse<Result>
            {
                Ok = data.OperationResult == OperationResult.Ok,
                Result = res
            };
        }

        internal class Result
        {
            public OperationResult OperationResult;
            public Dictionary<string, FsFolder> Container;
        }
    }
}
