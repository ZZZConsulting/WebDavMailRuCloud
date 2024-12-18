using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace YaR.Clouds.Base;

/// <summary>
/// Server file info.
/// </summary>
[DebuggerDisplay("{" + nameof(FullPath) + "}")]
public class Folder : IEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Folder" /> class.
    /// </summary>
    public Folder(string fullPath)
    {
        FullPath = WebDavPath.Clean(fullPath);
        Name = WebDavPath.Name(FullPath);

        //if (Name?.StartsWith(".") ?? false)
        //{
        //    if (Attributes.HasFlag(FileAttributes.Normal))
        //        Attributes = FileAttributes.Hidden;
        //    else
        //        Attributes |= FileAttributes.Hidden;
        //}
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Folder" /> class.
    /// </summary>
    /// <param name="size">Folder size.</param>
    /// <param name="fullPath">Full folder path.</param>
    /// <param name="publicLinks">Public folder link.</param>
    public Folder(FileSize size, string fullPath, IEnumerable<PublicLinkInfo> publicLinks = null)
        : this(fullPath)
    {
        Size = size;

        if (publicLinks != null)
        {
            foreach (var link in publicLinks)
                PublicLinks.TryAdd(link.Uri.AbsolutePath, link);
        }
    }

    /// <summary>
    /// Makes copy of this file with new path
    /// </summary>
    /// <param name="newFullPath"></param>
    /// <returns></returns>
    public virtual Folder New(string newFullPath, IEnumerable<IEntry> children = null)
    {
        var folder = new Folder(Size, newFullPath, null)
        {
            CreationTimeUtc = CreationTimeUtc,
            LastAccessTimeUtc = LastAccessTimeUtc,
            LastWriteTimeUtc = LastWriteTimeUtc,
            Attributes = Attributes,
            ServerFoldersCount = ServerFoldersCount,
            ServerFilesCount = ServerFilesCount,
            Descendants = children != null
                ? ImmutableList.Create(children.ToArray())
                : ImmutableList<IEntry>.Empty,
            IsChildrenLoaded = children != null
        };
        foreach (var linkPair in PublicLinks)
            folder.PublicLinks.AddOrUpdate(linkPair.Key, linkPair.Value, (_, _) => linkPair.Value);

        return folder;
    }

    /// <summary>
    /// Gets folder name.
    /// </summary>
    /// <value>Folder name.</value>
    public string Name { get; }

    /// <summary>
    /// Gets folder size.
    /// </summary>
    /// <value>Folder size.</value>
    public FileSize Size { get; }

    /// <summary>
    /// Gets full folder path on the server.
    /// </summary>
    /// <value>Full folder path.</value>
    public string FullPath { get; }


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


    public DateTime CreationTimeUtc { get; set; } = DateTime.Now.AddDays(-1);

    public DateTime LastWriteTimeUtc { get; set; } = DateTime.Now.AddDays(-1);


    public DateTime LastAccessTimeUtc { get; set; } = DateTime.Now.AddDays(-1);


    public FileAttributes Attributes { get; set; } = FileAttributes.Directory;

    public bool IsFile => false;

    public bool IsChildrenLoaded { get; internal set; } = false;

    public ImmutableList<IEntry> Descendants { get; set; } = ImmutableList<IEntry>.Empty;


    public int? ServerFoldersCount { get; set; }
    public int? ServerFilesCount { get; set; }

    public PublishInfo ToPublishInfo()
    {
        var info = new PublishInfo();
        if (!PublicLinks.IsEmpty)
            info.Items.Add(new PublishInfoItem { Path = FullPath, Urls = PublicLinks.Select(pli => pli.Value.Uri).ToList() });
        return info;
    }
}
