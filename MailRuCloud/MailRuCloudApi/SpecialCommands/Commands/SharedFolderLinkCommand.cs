using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YaR.Clouds.Base.Repos;
using YaR.Clouds.Base.Repos.MailRuCloud;
using YaR.Clouds.Links;

namespace YaR.Clouds.SpecialCommands.Commands;

public partial class SharedFolderLinkCommand : SpecialCommand
{
    public SharedFolderLinkCommand(Cloud cloud, string path, IList<string> parameters)
        : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(1, 2);


    private const string CommandRegexMask = @"(?snx-)\s* (?<url>(https://?cloud.mail.ru/public)?.*)/? \s*";
#if NET7_0_OR_GREATER
    [GeneratedRegex(CommandRegexMask)]
    private static partial Regex CommandRegex();
    private static readonly Regex s_commandRegex = CommandRegex();
#else
    private static readonly Regex s_commandRegex = new(CommandRegexMask, RegexOptions.Compiled);
#endif


    public override async Task<SpecialCommandResult> Execute()
    {
        var m = s_commandRegex.Match(_parameters[0]);

        if (!m.Success) return SpecialCommandResult.Fail;

        var publicBaseUrl = _cloud.RequestRepo.PublicBaseUrlDefault;
        var url = new Uri(m.Groups["url"].Value, UriKind.RelativeOrAbsolute);
        if (!url.IsAbsoluteUri)
            url = new Uri(publicBaseUrl + m.Groups["url"].Value, UriKind.Absolute);

        //TODO: make method in MailRuCloud to get entry by url
        //var item = await new ItemInfoRequest(Cloud.CloudApi, m.Groups["url"].Value, true).MakeRequestAsync(_connectionLimiter);

        var item = await _cloud.RequestRepo.ItemInfo(RemotePath.Get(new Link(url)));
        var entry = item.ToEntry(publicBaseUrl);
        if (entry is null)
            return SpecialCommandResult.Fail;

        string name = _parameters.Count > 1 && !string.IsNullOrWhiteSpace(_parameters[1])
                ? _parameters[1]
                : entry.Name;

        var res = await _cloud.LinkItem(
            url,
            _path, name, entry.IsFile, entry.Size, entry.CreationTimeUtc);

        return new SpecialCommandResult(res);
    }
}
