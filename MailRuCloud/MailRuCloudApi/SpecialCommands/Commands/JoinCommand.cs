using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YaR.Clouds.Base;
using YaR.Clouds.Base.Repos.MailRuCloud;

namespace YaR.Clouds.SpecialCommands.Commands;

public partial class JoinCommand : SpecialCommand
{
    private const string CommandRegexMask = @"(?snx-) (https://?cloud.mail.ru/public)?(?<data>/\w*/?\w*)/?\s*";
    private const string HashRegexMask = @"#(?<data>\w+)";
    private const string SizeRegexMask = @"(?<data>\w+)";
#if NET7_0_OR_GREATER
    [GeneratedRegex(CommandRegexMask)]
    private static partial Regex CommandRegex();
    private static readonly Regex s_commandRegex = CommandRegex();
    [GeneratedRegex(HashRegexMask)]
    private static partial Regex HashRegex();
    private static readonly Regex s_hashRegex = HashRegex();
    [GeneratedRegex(SizeRegexMask)]
    private static partial Regex SizeRegex();
    private static readonly Regex s_sizeRegex = SizeRegex();
#else
    private static readonly Regex s_commandRegex = new(CommandRegexMask, RegexOptions.Compiled);
    private static readonly Regex s_hashRegex = new(HashRegexMask, RegexOptions.Compiled);
    private static readonly Regex s_sizeRegex = new(SizeRegexMask, RegexOptions.Compiled);
#endif

    public JoinCommand(Cloud cloud, string path, IList<string> parameters) : base(cloud, path, parameters)
    {
        var m = s_commandRegex.Match(_parameters[0]);

        if (m.Success) //join by shared link
            _func = () => ExecuteByLink(_path, m.Groups["data"].Value);
        else
        {
            var mhash = s_hashRegex.Match(_parameters[0]);
            var msize = s_sizeRegex.Match(_parameters[1]);
            if (mhash.Success && msize.Success && _parameters.Count == 3) //join by hash and size
            {
                _func = () => ExecuteByHash(_path, mhash.Groups["data"].Value, long.Parse(_parameters[1]), _parameters[2]);
            }
        }
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(1, 3);

    private readonly Func<Task<SpecialCommandResult>> _func;

    public override Task<SpecialCommandResult> Execute()
    {
        return _func != null
            ? _func()
            : Task.FromResult(new SpecialCommandResult(false, "Invalid parameters"));
    }

    private async Task<SpecialCommandResult> ExecuteByLink(string path, string link)
    {
        var k = await _cloud.CloneItem(path, link);
        return new SpecialCommandResult(k.IsSuccess);
    }

    private async Task<SpecialCommandResult> ExecuteByHash(string path, string hash, long size, string paramPath)
    {
        string fpath = WebDavPath.IsFullPath(paramPath)
            ? paramPath
            : WebDavPath.Combine(path, paramPath);

        //TODO: now mail.ru only
        var k = await _cloud.AddFile(new FileHashMrc(hash), fpath, size, ConflictResolver.Rename);
        return new SpecialCommandResult(k.Success);
    }
}
