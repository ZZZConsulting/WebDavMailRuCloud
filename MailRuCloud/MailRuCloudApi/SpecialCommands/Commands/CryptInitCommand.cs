using System.Collections.Generic;
using System.Threading.Tasks;
using YaR.Clouds.Base;

namespace YaR.Clouds.SpecialCommands.Commands
{
    /// <summary>
    /// ������� ��� �������� �������, ��� ����� � �� ����� �����������
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
            string param = Parames.Count == 0 ? string.Empty : Parames[0].Replace("\\", WebDavPath.Separator);

            if (Parames.Count == 0)
                path = Path;
            else if (param.StartsWith(WebDavPath.Separator))
                path = param;
            else
                path = WebDavPath.Combine(Path, param);

            var entry = await Cloud.GetItemAsync(path);
            if (entry is null || entry.IsFile)
                return SpecialCommandResult.Fail;

            var res = await Cloud.CryptInit((Folder)entry);
            return new SpecialCommandResult(res);
        }
    }
}
