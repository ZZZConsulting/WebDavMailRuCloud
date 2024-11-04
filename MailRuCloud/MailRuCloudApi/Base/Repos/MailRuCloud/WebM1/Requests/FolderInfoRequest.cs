using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using YaR.Clouds.Base.Requests;
using YaR.Clouds.Base.Requests.Types;

namespace YaR.Clouds.Base.Repos.MailRuCloud.WebM1.Requests;

internal class FolderInfoRequest : BaseRequestJson<FolderInfoResult>
{
    private readonly string _path;
    private readonly bool _isWebLink;
    private readonly int _offset;
    private readonly int _limit;

    /*
     * Внимание!
     * При выборке с limit меньшим, чем количество файлов в папке,
     * Mail.Ru выдает список файлов от начала папки, игнорируя название файла в path.
     * Например, в папке файлы: a, b, c, d... z. Делаем выборку /x с limit равным 1. Сервер вернет файл a вместо x.
     *
     * Когда выборка делается с limit равным 1 или 2 вместо int.MaxValue,
     * это означает, что была одна из операций создания, удаления, переименования и т.д.,
     * после которой нужно подтверждение наличия файла или директории с соответствующим названием,
     * вместо всего списка выбирается только одно названия для подтверждения.
     * Из-за описанной выше проблемы при выборке одного названия
     * при обращении к серверу запрашивается информация типа file вместо folder,
     * а затем данные перекладываются в структуру, для которой давно есть обработка.
     */

    public FolderInfoRequest(HttpCommonSettings settings, IAuth auth, RemotePath path, int offset = 0, int limit = int.MaxValue)
        : base(settings, auth)
    {
        _isWebLink = path.IsLink;

        if (path.IsLink)
        {
            string ustr = path.Link.Href.OriginalString;
            _path = "/" + ustr.Remove(0, ustr.IndexOf("/public/", StringComparison.Ordinal) + "/public/".Length);
        }
        else
        {
            _path = path.Path;
        }

        _offset = offset;
        _limit = limit;
    }

    protected override string RelationalUri
        => _limit <= 2
           ? $"/api/m1/file?access_token={_auth.AccessToken}"
           : $"/api/m1/folder?access_token={_auth.AccessToken}&offset={_offset}&limit={_limit}";

    protected override byte[] CreateHttpContent()
    {
        // path sent using POST cause of unprintable Unicode characters may exists
        // https://github.com/yar229/WebDavMailRuCloud/issues/54
        var data = _isWebLink
            ? $"weblink={_path}"
            : $"home={Uri.EscapeDataString(_path)}";
        return Encoding.UTF8.GetBytes(data);
    }

    protected override RequestResponse<FolderInfoResult> DeserializeMessage(NameValueCollection responseHeaders, Stream stream)
    {
        RequestResponse<FolderInfoResult> response = base.DeserializeMessage(responseHeaders, stream);

        if (_limit > 2)
            return response;

        #region Данные об одиночном файле или папке нужно переложить структуру списка

        FolderInfoResult.FolderInfoBody body = null;
        FolderInfoResult folderInfoResult = null;
        if (response.Result is not null)
        {
            if (response.Result.Body is not null)
            {
                string home = response.Result.Body.Home;
                string name = response.Result.Body.Name;
                if (response.Result.Body.Kind == "file")
                {
                    home = WebDavPath.Parent(response.Result.Body.Home);
                    name = WebDavPath.Name(home);
                }

                FolderInfoResult.FolderInfoBody.FolderInfoProps item = new()
                {
                    Weblink = response.Result.Body.Weblink,
                    Size = response.Result.Body.Size,
                    Name = response.Result.Body.Name,
                    Home = response.Result.Body.Home,
                    Kind = response.Result.Body.Kind,
                    Hash = response.Result.Body.Hash,
                    Mtime = response.Result.Body.Mtime,
                };
                body = new()
                {
                    Name = name,
                    Home = home,
                    Kind = response.Result.Body.Kind,
                    Hash = response.Result.Body.Hash,
                    Mtime = response.Result.Body.Mtime,
                    Size = response.Result.Body.Size,
                    Weblink = response.Result.Body.Weblink,
                    List = [item],
                    Count = new FolderInfoResult.FolderInfoBody.FolderInfoCount()
                    {
                        Files = response.Result.Body.Kind == "file" ? 1 : 0,
                        Folders = response.Result.Body.Kind == "folder" ? 1 : 0,
                    },
                };
            }
            folderInfoResult = new FolderInfoResult()
            {
                Time = response.Result.Time,
                Email = response.Result.Email,
                Status = response.Result.Status,
                Body = body,
            };
        }
        RequestResponse<FolderInfoResult> dataRes = new()
        {
            Ok = response.Ok,
            ErrorCode = response.ErrorCode,
            Description = response.Description,
            Result = folderInfoResult,
        };

        return dataRes;

        #endregion
    }
}
