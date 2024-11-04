using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YaR.Clouds.Base;
using YaR.Clouds.Base.Repos;
using YaR.Clouds.Links;

namespace YaR.Clouds.SpecialCommands.Commands;

public class ListCommand : SpecialCommand
{
    private const string FileListExtention = ".wdmrc.list.lst";

    //private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(FishCommand));

    public ListCommand(Cloud cloud, string path, IList<string> parameters) : base(cloud, path, parameters)
    {
    }

    protected override MinMax<int> MinMaxParamsCount { get; } = new(0, 1);

    public override async Task<SpecialCommandResult> Execute()
    {
        string target = _parameters.Count > 0 && !string.IsNullOrWhiteSpace(_parameters[0])
            ? _parameters[0].StartsWith(WebDavPath.Separator) ? _parameters[0] : WebDavPath.Combine(_path, _parameters[0])
            : _path;

        var resolvedTarget = await RemotePath.Get(target, _cloud.LinkManager);
        var entry = await _cloud.RequestRepo.FolderInfo(resolvedTarget);
        string resFilepath = WebDavPath.Combine(_path, string.Concat(entry.Name, FileListExtention));

        var sb = new StringBuilder();

        foreach (var e in Flat(entry, _cloud.LinkManager))
        {
            string hash = (e as File)?.Hash.ToString() ?? "-";
            string link = e.PublicLinks.Values.FirstOrDefault()?.Uri.OriginalString ?? "-";
            sb.AppendLine(
                $"{e.FullPath}\t{e.Size.DefaultValue}\t{e.CreationTimeUtc:yyyy.MM.dd HH:mm:ss}\t{hash}\t{link}");
        }

        _cloud.UploadFile(resFilepath, sb.ToString());

        return SpecialCommandResult.Success;
    }

    private IEnumerable<IEntry> Flat(IEntry entry, LinkManager lm)
    {
        yield return entry;

        var ifolders = entry.Descendants
            .AsParallel()
            .WithDegreeOfParallelism(5)
            .Select(it => it switch
            {
                File => it,
                Folder ifolder => ifolder.IsChildrenLoaded
                    ? ifolder
                    : _cloud.RequestRepo.FolderInfo(RemotePath.Get(it.FullPath, lm).Result, depth: 3).Result,
                _ => throw new NotImplementedException("Unknown item type")
            })
            .OrderBy(it => it.Name);

        foreach (var item in ifolders.SelectMany(f => Flat(f, lm)))
            yield return item;
    }
}
