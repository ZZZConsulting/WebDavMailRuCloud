using System.Collections.Generic;
using System.Threading.Tasks;

namespace YaR.Clouds.SpecialCommands.Commands;

public class RemoveBadLinksCommand : SpecialCommand
{
    public RemoveBadLinksCommand(Cloud cloud, string path, IList<string> parameters)
        : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(0);

    public override Task<SpecialCommandResult> Execute()
    {
        _cloud.RemoveDeadLinks();
        return Task.FromResult(SpecialCommandResult.Success);
    }
}
