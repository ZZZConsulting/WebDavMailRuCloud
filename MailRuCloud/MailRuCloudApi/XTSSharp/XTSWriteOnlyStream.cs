﻿using System;
using System.IO;

namespace YaR.Clouds.XTSSharp;

class XTSWriteOnlyStream : Stream
{
    public const int DefaultSectorSize = 512;
    public const int BlockSize = 16;

    private readonly XtsCryptoTransform _encryptor;
    private readonly Stream _baseStream;

    private ulong _currentSector;
    private readonly int _sectorSize;
    private readonly byte[] _encriptedBuffer;
    private readonly byte[] _sectorBuffer;

    /// <summary>
    /// Creates a new stream
    /// </summary>
    /// <param name="baseStream">The base stream</param>
    /// <param name="xts">The xts transform</param>
    /// <param name="sectorSize">Sector size</param>
    public XTSWriteOnlyStream(Stream baseStream, Xts xts, int sectorSize = DefaultSectorSize)
    {
        _baseStream = baseStream;

        _encryptor = xts.CreateEncryptor();

        _sectorSize = sectorSize;
        _encriptedBuffer = new byte[sectorSize];
        _sectorBuffer = new byte[sectorSize];
    }

    public Action FileStreamSent;
    private void OnFileStreamSent() => FileStreamSent?.Invoke();

    public Action ServerFileProcessed;
    private void OnServerFileProcessed() => ServerFileProcessed?.Invoke();


    private int _sectorBufferCount;

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count == 0)
            return;

        while (count > 0)
        {
            int bytesToCopy = Math.Min(count, _sectorSize - _sectorBufferCount);
            Buffer.BlockCopy(buffer, offset, _sectorBuffer, _sectorBufferCount, bytesToCopy);
            _sectorBufferCount += bytesToCopy;
            offset += bytesToCopy;
            count -= bytesToCopy;

            if (_sectorBufferCount != _sectorSize)
                continue;

            //sector filled
            _encryptor.TransformBlock(_sectorBuffer, 0, _sectorSize, _encriptedBuffer, 0, _currentSector);
            _baseStream.Write(_encriptedBuffer, 0, _sectorSize);

            _currentSector++;
            _sectorBufferCount = 0;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        if (_sectorBufferCount > 0)
        {
            int towrite = _sectorBufferCount % BlockSize == 0
                ? _sectorBufferCount
                : (_sectorBufferCount / BlockSize + 1) * BlockSize;

            _encryptor.TransformBlock(_sectorBuffer, 0, towrite, _encriptedBuffer, 0, _currentSector);
            _baseStream.Write(_encriptedBuffer, 0, towrite);

        }

        OnFileStreamSent();

        _baseStream.Dispose();
        _encryptor.Dispose();

        OnServerFileProcessed();
    }


    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length { get; }
    public override long Position { get; set; }
}
