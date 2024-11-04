using System.Collections.Generic;
using System.Threading.Tasks;
using YaR.Clouds.Base;

namespace YaR.Clouds.SpecialCommands.Commands;

public class MoveCommand : SpecialCommand
{
    public MoveCommand(Cloud cloud, string path, IList<string> parameters)
        : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(1, 2);

    public override async Task<SpecialCommandResult> Execute()
    {
        string source = WebDavPath.Clean(_parameters.Count == 1 ? _path : _parameters[0]);
        string target = WebDavPath.Clean(_parameters.Count == 1 ? _parameters[0] : _parameters[1]);

        var entry = await _cloud.GetItemAsync(source);
        if (entry is null)
            return SpecialCommandResult.Fail;

        var res = await _cloud.MoveAsync(entry, target);
        return new SpecialCommandResult(res);
    }
}
