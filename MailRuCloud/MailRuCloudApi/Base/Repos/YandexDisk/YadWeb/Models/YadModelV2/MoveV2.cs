using System.Collections.Generic;
using System.Linq;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2Move : YadModelV2
{
    public YadModelV2Move(string sourcePath, string destPath)
    {
        APIMethod = "mpfs/bulk-async-move";
        ResultType = typeof(List<YadResponseV2OperationStatus>);
        Src = sourcePath;
        Dst = destPath;
        RequestParameter = () => new YadRequestV2Operations()
        {
            Operations =
            [
                new YadRequestV2Operation()
                {
                    Src = WebDavPath.Combine("/disk", sourcePath),
                    Dst = WebDavPath.Combine("/disk", destPath),
                }
            ]
        };
    }

    public string Src { get; }
    public string Dst { get; }

    public string OperationId
        => ((List<YadResponseV2OperationStatus>)ResultObject)?.FirstOrDefault()?.Oid;
}
