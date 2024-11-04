using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using YaR.Clouds.Common;

namespace YaR.Clouds.Base;

/// <summary>
/// Server file info.
/// </summary>
[DebuggerDisplay("{" + nameof(FullPath) + "}")]
public class File : IEntry
{
    protected File()
    {
    }

    public File(string fullPath, long size, IFileHash hash = null)
    {
        FullPath = fullPath;
        ServiceInfo = FilenameServiceInfo.Parse(WebDavPath.Name(fullPath));

        _originalSize = size;
        _hash = hash;
    }

    public File(string fullPath, long size, params PublicLinkInfo[] links)
        : this(fullPath, size)
    {
        foreach (var link in links)
            PublicLinks.AddOrUpdate(link.Uri.AbsolutePath, link, (_, _) => link);
    }

    private IFileHash _hash;

    /// <summary>
    /// Кеш URL на скачивание файла с сервера.
    /// Заполняется операцией GetDownloadStream,
    /// чтобы при повторном обращении на чтение файла не тратить время на получения URL'а.
    /// </summary>
    public string DownloadUrlCache { get; set; } = null;
    /// <summary>
    /// Время, с которого кешем <see cref="DownloadUrlCache"/> пользоваться нельзя
    /// и нужно получить новый URL.
    /// </summary>
    public DateTime DownloadUrlCacheExpirationTime = DateTime.MinValue;


    /// <summary>
    /// makes copy of this file with new path
    /// </summary>
    /// <param name="newFullPath"></param>
    /// <returns></returns>
    public virtual File New(string newFullPath)
    {
        var file = new File(newFullPath, Size, Hash)
        {
            CreationTimeUtc = CreationTimeUtc,
            LastAccessTimeUtc = LastAccessTimeUtc,
            LastWriteTimeUtc = LastWriteTimeUtc
        };
        foreach (var linkPair in PublicLinks)
            file.PublicLinks.AddOrUpdate(linkPair.Key, linkPair.Value, (_, _) => linkPair.Value);

        return file;
    }

    /// <summary>
    /// Gets file name.
    /// </summary>
    /// <value>File name.</value>
    public string Name
    {
        get => _name;
        private set
        {
            _name = value;

            Extension = System.IO.Path.GetExtension(_name)?.TrimStart('.') ?? string.Empty;
        }
    } //WebDavPath.Name(FullPath)
    private string _name;

    /// <summary>
    /// Gets file extension (without ".")
    /// </summary>
    public string Extension { get; private set; }

    /// <summary>
    /// Gets file hash value.
    /// </summary>
    /// <value>File hash.</value>
    public virtual IFileHash Hash
    {
        get => _hash;
        internal set => _hash = value;
    }

    /// <summary>
    /// Gets file size.
    /// </summary>
    /// <value>File size.</value>
    public virtual FileSize Size => OriginalSize - (ServiceInfo.CryptInfo?.AlignBytes ?? 0);

    public virtual FileSize OriginalSize
    {
        get => _originalSize;
        set => _originalSize = value;
    }
    private FileSize _originalSize;

    protected virtual File FileHeader => null;

    /// <summary>
    /// Gets full file path with name on server.
    /// </summary>
    /// <value>Full file path.</value>
    public string FullPath
    {
        get => _fullPath;
        protected set
        {
            _fullPath = WebDavPath.Clean(value);
            Name = WebDavPath.Name(_fullPath);
            Path = WebDavPath.Parent(_fullPath);
        }
    }

    private string _fullPath;

    /// <summary>
    /// Path to file (without filename)
    /// </summary>
    public string Path { get; private set; }

    /// <summary>
    /// Gets public file link.
    /// </summary>
    /// <value>Public link.</value>
    public ConcurrentDictionary<string, PublicLinkInfo> PublicLinks
        => _publicLinks ??= new ConcurrentDictionary<string, PublicLinkInfo>(StringComparer.InvariantCultureIgnoreCase);

    private ConcurrentDictionary<string, PublicLinkInfo> _publicLinks;

    public IEnumerable<PublicLinkInfo> GetPublicLinks(Cloud cloud)
    {
        return PublicLinks.IsEmpty
            ? cloud.GetSharedLinks(FullPath)
            : PublicLinks.Values;
    }

    public ImmutableList<IEntry> Descendants => ImmutableList<IEntry>.Empty;

    /// <summary>
    /// List of physical files contains data
    /// </summary>
    public virtual List<File> Parts => new() { this };
    public virtual IList<File> Files => new List<File> { this };

    private static readonly DateTime MinFileDate = new(1900, 1, 1);
    public virtual DateTime CreationTimeUtc { get; set; } = MinFileDate;
    public virtual DateTime LastWriteTimeUtc { get; set; } = MinFileDate;
    public virtual DateTime LastAccessTimeUtc { get; set; } = MinFileDate;

    public FileAttributes Attributes { get; set; } = FileAttributes.Normal;

    public bool IsFile => true;
    public FilenameServiceInfo ServiceInfo { get; protected set; }

    //TODO : refact, bad design
    public void SetName(string destinationName)
    {
        FullPath = WebDavPath.Combine(Path, destinationName);
        if (ServiceInfo != null) ServiceInfo.CleanName = Name;

        if (Files.Count <= 1)
            return;

        string path = Path;
        foreach (var fiFile in Parts)
            fiFile.FullPath = WebDavPath.Combine(
                path,
                string.Concat(destinationName, fiFile.ServiceInfo.ToString(false))); //TODO: refact
    }

    //TODO : refact, bad design
    public void SetPath(string fullPath)
    {
        FullPath = WebDavPath.Combine(fullPath, Name);
        if (Parts.Count <= 1)
            return;

        foreach (var fiFile in Parts)
            fiFile.FullPath = WebDavPath.Combine(fullPath, fiFile.Name); //TODO: refact
    }


    //TODO : refact, bad design
    public CryptoKeyInfo EnsurePublicKey(Cloud cloud)
    {
        if (!ServiceInfo.IsCrypted || null != ServiceInfo.CryptInfo.PublicKey)
            return ServiceInfo.CryptInfo.PublicKey;

        var info = cloud.DownloadFileAsJson<HeaderFileContent>(FileHeader ?? this);
        ServiceInfo.CryptInfo.PublicKey = info.PublicKey;
        return ServiceInfo.CryptInfo.PublicKey;
    }

    public PublishInfo ToPublishInfo(Cloud cloud, bool generateDirectVideoLink, SharedVideoResolution videoResolution)
    {
        var info = new PublishInfo();

        bool isSplitted = Files.Count > 1;

        int cnt = 0;
        foreach (var innerFile in Files)
        {
            if (!innerFile.PublicLinks.IsEmpty)
                info.Items.Add(new PublishInfoItem
                {
                    Path = innerFile.FullPath,
                    Urls = innerFile.PublicLinks.Select(pli => pli.Value.Uri).ToList(),
                    PlayListUrl = !isSplitted || cnt > 0
                                      ? generateDirectVideoLink
                                            ? File.ConvertToVideoLink(cloud, innerFile.PublicLinks.Values.FirstOrDefault()?.Uri, videoResolution)
                                            : null
                                      : null
                });
            cnt++;
        }

        return info;
    }

    private static string ConvertToVideoLink(Cloud cloud, Uri publicLink, SharedVideoResolution videoResolution)
    {
        return cloud.RequestRepo.ConvertToVideoLink(publicLink, videoResolution);

        //    GetShardInfo(ShardType.WeblinkVideo).Result.Url +
        //videoResolution.ToEnumMemberValue() + "/" + //"0p/" +
        //Base64Encode(publicLink.TrimStart('/')) +
        //".m3u8?double_encode=1";
    }
}
