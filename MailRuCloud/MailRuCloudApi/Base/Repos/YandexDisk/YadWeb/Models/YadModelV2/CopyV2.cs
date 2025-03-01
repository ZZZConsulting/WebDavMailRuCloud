using System.Collections.Generic;
using System.Linq;

namespace YaR.Clouds.Base.Repos.YandexDisk.YadWeb.Models;

internal class YadModelV2Copy : YadModelV2
{
    public YadModelV2Copy(string sourcePath, string destPath, bool force = true)
    {
        APIMethod = "mpfs/bulk-async-copy";
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
                    Force = force? 1 : null
                }
            ]
        };
    }

    public string Src { get; }
    public string Dst { get; }

    public string OperationId
        => ((List<YadResponseV2OperationStatus>)ResultObject)?.FirstOrDefault()?.Oid;
}
