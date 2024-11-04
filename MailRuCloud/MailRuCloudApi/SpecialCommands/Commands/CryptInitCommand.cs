using System.Collections.Generic;
using System.Threading.Tasks;
using YaR.Clouds.Base;

namespace YaR.Clouds.SpecialCommands.Commands;

/// <summary>
/// Создает для каталога признак, что файлы в нём будут шифроваться
/// </summary>
public class CryptInitCommand : SpecialCommand
{
    public CryptInitCommand(Cloud cloud, string path, IList<string> parameters)
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
        if (entry is null || entry.IsFile)
            return SpecialCommandResult.Fail;

        var res = await _cloud.CryptInit((Folder)entry);
        return new SpecialCommandResult(res);
    }
}
