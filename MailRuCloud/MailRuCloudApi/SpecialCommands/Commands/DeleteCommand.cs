using System.Collections.Generic;
using System.Threading.Tasks;
using YaR.Clouds.Base;

namespace YaR.Clouds.SpecialCommands.Commands;

public class DeleteCommand : SpecialCommand
{
    public DeleteCommand(Cloud cloud, string path, IList<string> parameters)
        : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(0, 1);

    public override async Task<SpecialCommandResult> Execute()
    {
        string path;
        string param = _parameters.Count == 0 ? string.Empty : _parameters[0].Replace("\\", WebDavPath.Separator);

        if (_parameters.Count == 0)
            path = _path;
        else if (param.StartsWith(WebDavPath.Separator))
            path = param;
        else
            path = WebDavPath.Combine(_path, param);

        var entry = await _cloud.GetItemAsync(path);
        if (entry is null)
            return SpecialCommandResult.Fail;

        var res = await _cloud.Remove(entry);
        return new SpecialCommandResult(res);
    }
}
