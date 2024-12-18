using System;
using System.IO;
using System.Threading.Tasks;
using YaR.Clouds.Base;
using YaR.Clouds.XTSSharp;
using File = YaR.Clouds.Base.File;

namespace YaR.Clouds.Streams;

public class UploadStreamFabric
{
    private readonly Cloud _cloud;

    public UploadStreamFabric(Cloud cloud)
    {
        _cloud = cloud;
    }

    public Action FileStreamSent;
    public Action ServerFileProcessed;

    public async Task<Stream> Create(File file, FileUploadedDelegate onUploaded = null, bool discardEncryption = false)
    {
        string folderPath = file.Path;
        IEntry entry = await _cloud.GetItemAsync(file.Path, Cloud.ItemType.Folder);
        if (entry is null || entry is not Folder folder)
            throw new DirectoryNotFoundException(file.Path);

        Stream stream;

        string fullPath = _cloud.Find(CryptFileInfo.FileName, WebDavPath.GetParents(folder.FullPath).ToArray());
        bool cryptRequired = !string.IsNullOrEmpty(fullPath);
        if (cryptRequired && !discardEncryption)
        {
            if (!_cloud.Credentials.CanCrypt)
                throw new Exception($"Cannot upload {file.FullPath} to crypt folder without additional password!");

            // #142 remove crypted file parts if size changed
            IEntry checkExistance = await _cloud.GetItemAsync(file.FullPath, Cloud.ItemType.File);
            if(checkExistance is not null)
                await _cloud.Remove(file.FullPath);

            stream = GetCryptoStream(file, onUploaded);
        }
        else
        {
            stream = GetPlainStream(file, onUploaded);
        }

        return stream;
    }

    private Stream GetPlainStream(File file, FileUploadedDelegate onUploaded)
    {
        var stream = new SplittedUploadStream(file.FullPath, _cloud, file.Size, FileStreamSent, ServerFileProcessed);
        if (onUploaded is not null)
            stream.FileUploaded += onUploaded;
        return stream;
    }

    private Stream GetCryptoStream(File file, FileUploadedDelegate onUploaded)
    {
        var info = CryptoUtil.GetCryptoKeyAndSalt(_cloud.Credentials.PasswordCrypt);
        var xts = XtsAes256.Create(info.Key, info.IV);

        file.ServiceInfo.CryptInfo = new CryptInfo
        {
            PublicKey = new CryptoKeyInfo { Salt = info.Salt, IV = info.IV },
            AlignBytes = (uint)(file.Size % XTSWriteOnlyStream.BlockSize != 0
                ? XTSWriteOnlyStream.BlockSize - file.Size % XTSWriteOnlyStream.BlockSize
                : 0)
        };

        var size = file.OriginalSize % XTSWriteOnlyStream.BlockSize == 0
            ? file.OriginalSize.DefaultValue
            : (file.OriginalSize / XTSWriteOnlyStream.BlockSize + 1) * XTSWriteOnlyStream.BlockSize;

        var ustream = new SplittedUploadStream(file.FullPath, _cloud, size, null, null, false, file.ServiceInfo.CryptInfo);
        if (onUploaded is not null)
            ustream.FileUploaded += onUploaded;
        // ReSharper disable once RedundantArgumentDefaultValue
        var encustream = new XTSWriteOnlyStream(ustream, xts, XTSWriteOnlyStream.DefaultSectorSize)
        {
            FileStreamSent = FileStreamSent,
            ServerFileProcessed = ServerFileProcessed
        };

        return encustream;
    }
}
