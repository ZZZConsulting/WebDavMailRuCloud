using System.Collections.Generic;
using System.Threading.Tasks;

namespace YaR.Clouds.SpecialCommands.Commands;

/// <summary>
/// Пароль для (де)шифрования
/// </summary>
public class CryptPasswdCommand : SpecialCommand
{
    public CryptPasswdCommand(Cloud cloud, string path, IList<string> parameters)
        : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(1);

    public override async Task<SpecialCommandResult> Execute()
    {
        var newPasswd = _parameters[0];
        if (string.IsNullOrEmpty(newPasswd))
            return await Task.FromResult(new SpecialCommandResult(false, "Crypt password is empty"));

        _cloud.Credentials.PasswordCrypt = newPasswd;

        return await Task.FromResult(SpecialCommandResult.Success);
    }
}
