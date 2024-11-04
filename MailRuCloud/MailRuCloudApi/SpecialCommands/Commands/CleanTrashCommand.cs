using System.Collections.Generic;
using System.Threading.Tasks;

namespace YaR.Clouds.SpecialCommands.Commands;

/// <summary>
/// Очистка корзины
/// </summary>
public class CleanTrashCommand : SpecialCommand
{
    public CleanTrashCommand(Cloud cloud, string path, IList<string> parameters)
        : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(0);

    public override async Task<SpecialCommandResult> Execute()
    {
        _cloud.CleanTrash();

        return await Task.FromResult(SpecialCommandResult.Success);
    }
}
