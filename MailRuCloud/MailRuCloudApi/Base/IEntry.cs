using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace YaR.Clouds.Base;

public interface IEntry
{
    bool IsFile { get; }
    FileSize Size { get; }
    string Name { get; }
    string FullPath { get; }
    DateTime CreationTimeUtc { get; }
    ConcurrentDictionary<string, PublicLinkInfo> PublicLinks { get; }
    ImmutableList<IEntry> Descendants { get; }
}
