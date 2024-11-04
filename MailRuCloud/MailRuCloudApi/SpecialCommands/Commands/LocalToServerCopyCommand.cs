using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YaR.Clouds.Base;

namespace YaR.Clouds.SpecialCommands.Commands;

public class LocalToServerCopyCommand : SpecialCommand
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(LocalToServerCopyCommand));

    public LocalToServerCopyCommand(Cloud cloud, string path, IList<string> parameters) : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(1);

    public override async Task<SpecialCommandResult> Execute()
    {
        var res = await Task.Run(async () =>
        {
            var sourceFileInfo = new FileInfo(_parameters[0]);

            string name = sourceFileInfo.Name;
            string targetPath = WebDavPath.Combine(_path, name);

            Logger.Info($"COMMAND:COPY:{_parameters[0]}");

            using (var source = System.IO.File.Open(_parameters[0], FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var target = await _cloud.GetFileUploadStream(targetPath, sourceFileInfo.Length, null, null).ConfigureAwait(false))
            {
                await source.CopyToAsync(target);
            }

            return SpecialCommandResult.Success;
        });

        return res;
    }
}
