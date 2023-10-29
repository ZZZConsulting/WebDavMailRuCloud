﻿//using System;
//using System.IO;
//using System.Net;
//using System.Threading.Tasks;
//using YaR.MailRuCloud.Api.Base.Repos;
//using YaR.MailRuCloud.Api.Base.Requests;
//using YaR.MailRuCloud.Api.Base.Requests.Types;
//using YaR.MailRuCloud.Api.Extensions;

//namespace YaR.MailRuCloud.Api.Base.Streams
//{
//    abstract class UploadStreamHttpWebRequestV2 : Stream
//    {
//        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(UploadStream));

//        protected UploadStreamHttpWebRequestV2(string destinationPath, MailRuCloud cloud, long size)
//        {
//            _cloud = cloud;
//            _file = new File(destinationPath, size, null);

//            Initialize();
//        }

//        private void Initialize()
//        {
//            _requestTask = Task.Run(async () =>
//            {
//                try
//                {
//                    var boundary = new UploadMultipartBoundary(_file);
//                    //var shard = _cloud.RequestRepo.GetShardInfo(ShardType.Upload).Result;
//                    _request = _cloud.RequestRepo.UploadRequest(_file, boundary);

//                    Logger.Debug($"HTTP:{_request.Method}:{_request.RequestUri.AbsoluteUri}");

//                    using (var requeststream = await _request.GetRequestStreamAsync())
//                    {
//                        await requeststream.WriteAsync(boundary.Start, 0, boundary.Start.Length);
//                        await _ringBuffer.CopyToAsync(requeststream);
//                        await requeststream.WriteAsync(boundary.End, 0, boundary.End.Length);
//                    }

//                    var response = _request.GetResponse();
//                    return (HttpWebResponse)response;
//                }
//                catch (Exception e)
//                {
//                    Logger.Error("Upload request failed", e);
//                    throw;
//                }
//            });
//        }

//        public bool CheckHashes { get; set; } = true;

//        public override void Write(byte[] buffer, int offset, int count)
//        {
//            if (CheckHashes)
//                _sha1.Append(buffer, offset, count);

//            _ringBuffer.Write(buffer, offset, count);
//        }

//        protected override void Dispose(bool disposing)
//        {
//            base.Dispose(disposing);
//            if (!disposing) return;

//            try
//            {
//                _ringBuffer.Flush();

//                using (var response = _requestTask.Result)
//                {
//                    if (response.StatusCode != HttpStatusCode.OK)
//                        throw new Exception("Cannot upload file, status " + response.StatusCode);

//                    var ures = response.ReadAsText(_cloud.CancelToken)
//                        .ToUploadPathResult();

//                    _file.OriginalSize = ures.Size;
//                    _file.Hash = ures.Hash;

//                    if (CheckHashes && _sha1.HashString != ures.Hash)
//                        throw new HashMatchException(_sha1.HashString, ures.Hash);

//                    _cloud.AddFileInCloud(_file, ConflictResolver.Rewrite)
//                        .Result
//                        .ThrowIf(r => !r.Success, r => new Exception("Cannot add file"));
//                }
//            }
//            finally
//            {
//                _ringBuffer?.Dispose();
//                _sha1?.Dispose();
//            }
//        }

//        private readonly MailRuCloud _cloud;
//        private readonly File _file;

//        private readonly MailRuSha1Hash _sha1 = new MailRuSha1Hash();
//        private HttpWebRequest _request;
//        private Task<HttpWebResponse> _requestTask;
//        private readonly RingBufferedStream _ringBuffer = new RingBufferedStream(65536);

//        //===========================================================================================================================

//        public override bool CanRead => true;
//        public override bool CanSeek => true;
//        public override bool CanWrite => true;
//        public override long Length => _file.OriginalSize;
//        public override long Position { get; set; }

//        public override void SetLength(long value)
//        {
//            _file.OriginalSize = value;
//        }

//        public override void Flush()
//        {
//            throw new NotImplementedException();
//        }

//        public override long Seek(long offset, SeekOrigin origin)
//        {
//            throw new NotImplementedException();
//        }

//        public override int Read(byte[] buffer, int offset, int count)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}