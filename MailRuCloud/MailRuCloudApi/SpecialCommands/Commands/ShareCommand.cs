using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YaR.Clouds.Base;
using YaR.Clouds.Common;
using YaR.Clouds.Extensions;

namespace YaR.Clouds.SpecialCommands.Commands;

public class ShareCommand : SpecialCommand
{

    public ShareCommand(Cloud cloud, string path, bool generateDirectVideoLink, bool makeM3UFile, IList<string> parameters)
        : base(cloud, path, parameters)
    {
        _generateDirectVideoLink = generateDirectVideoLink;
        _makeM3UFile = makeM3UFile;
    }


    private readonly bool _generateDirectVideoLink;
    private readonly bool _makeM3UFile;

    protected override MinMax<int> MinMaxParamsCount { get; } = new(0, 2);

    public override async Task<SpecialCommandResult> Execute()
    {
        string path;
        string param = _parameters.Count == 0
            ? string.Empty
            : _parameters[0].Replace("\\", WebDavPath.Separator);
        SharedVideoResolution videoResolution = _parameters.Count < 2
            ? _cloud.Settings.DefaultSharedVideoResolution
            : EnumExtensions.ParseEnumMemberValue<SharedVideoResolution>(_parameters[1]);

        if (_parameters.Count == 0)
            path = _path;
        else if (param.StartsWith(WebDavPath.Separator))
            path = param;
        else
            path = WebDavPath.Combine(_path, param);

        var entry = await _cloud.GetItemAsync(path);
        if (entry is null)
            return SpecialCommandResult.Fail;

        try
        {
            await _cloud.Publish(entry, true, _generateDirectVideoLink, _makeM3UFile, videoResolution);
        }
        catch (Exception e)
        {
            return new SpecialCommandResult(false, e.Message);
        }

        return SpecialCommandResult.Success;
    }
}
