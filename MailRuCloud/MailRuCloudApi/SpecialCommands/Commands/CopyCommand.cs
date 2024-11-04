using System.Collections.Generic;
using System.Threading.Tasks;
using YaR.Clouds.Base;

namespace YaR.Clouds.SpecialCommands.Commands;

public class CopyCommand : SpecialCommand
{
    public CopyCommand(Cloud cloud, string path, IList<string> parameters) : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(1, 2);

    public override async Task<SpecialCommandResult> Execute()
    {
        string source = WebDavPath.Clean(_parameters.Count == 1 ? _path : _parameters[0]);
        string target = WebDavPath.Clean(_parameters.Count == 1 ? _parameters[0] : _parameters[1]);

        var res = await _cloud.Copy(source, target);
        return new SpecialCommandResult(res);

    }
}
