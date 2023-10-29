using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YaR.Clouds.Base;

namespace YaR.Clouds.SpecialCommands.Commands
{
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
                var sourceFileInfo = new FileInfo(Parames[0]);

                string name = sourceFileInfo.Name;
                string targetPath = WebDavPath.Combine(Path, name);

                Logger.Info($"COMMAND:COPY:{Parames[0]}");

                using (var source = System.IO.File.Open(Parames[0], FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var target = await Cloud.GetFileUploadStream(targetPath, sourceFileInfo.Length, null, null).ConfigureAwait(false))
                {
                    await source.CopyToAsync(target);
                }

                return SpecialCommandResult.Success;
            });

            return res;
        }
    }
}