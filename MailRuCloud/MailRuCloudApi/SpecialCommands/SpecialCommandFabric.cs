using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using YaR.Clouds.Base;
using YaR.Clouds.SpecialCommands.Commands;

namespace YaR.Clouds.SpecialCommands;

/// <summary>
/// Обрабатывает командную строку и возвращает нужный объект команды
/// </summary>
public partial class SpecialCommandFabric
{
    private static readonly List<SpecialCommandContainer> CommandContainers = new()
    {
        new()
        {
            Commands = new [] {"del"},
            CreateFunc = (cloud, path, param) => new DeleteCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"link"},
            CreateFunc = (cloud, path, param) => new SharedFolderLinkCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"link", "check"},
            CreateFunc = (cloud, path, param) => new RemoveBadLinksCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"join"},
            CreateFunc = (cloud, path, param) => new JoinCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"copy"},
            CreateFunc = (cloud, path, param) => new CopyCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"move"},
            CreateFunc = (cloud, path, param) => new MoveCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"fish"},
            CreateFunc = (cloud, path, param) => new FishCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"lcopy"},
            CreateFunc = (cloud, path, param) => new LocalToServerCopyCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"crypt", "init"},
            CreateFunc = (cloud, path, param) => new CryptInitCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"crypt", "passwd"},
            CreateFunc = (cloud, path, param) => new CryptPasswdCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"share"},
            CreateFunc = (cloud, path, param) => new ShareCommand(cloud, path, false, false, param)
        },
        new()
        {
            Commands = new [] {"sharev"},
            CreateFunc = (cloud, path, param) => new ShareCommand(cloud, path, true, false, param)
        },
        new()
        {
            Commands = new [] {"pl"},
            CreateFunc = (cloud, path, param) => new ShareCommand(cloud, path, true, true, param)
        },
        new()
        {
            Commands = new [] {"rlist"},
            CreateFunc = (cloud, path, param) => new ListCommand(cloud, path, param)
        },
        new()
        {
            Commands = new [] {"clean", "trash"},
            CreateFunc = (cloud, path, param) => new CleanTrashCommand(cloud, path, param)
        },

        new()
        {
            Commands = new [] {"test"},
            CreateFunc = (cloud, path, param) => new TestCommand(cloud, path, param)
        }
    };


    public SpecialCommand Build(Cloud cloud, string param)
    {
        var res = ParseLine(param, cloud.Settings.SpecialCommandPrefix);
        if (!res.IsValid && !string.IsNullOrEmpty(cloud.Settings.AdditionalSpecialCommandPrefix))
            res = ParseLine(param, cloud.Settings.AdditionalSpecialCommandPrefix);
        if (!res.IsValid)
            return null;

        var parameters = ParseParameters(res.Data);
        var commandContainer = FindCommandContainer(parameters);
        if (commandContainer == null) return null;

        parameters = parameters.Skip(commandContainer.Commands.Length).ToList();
        var cmd = commandContainer.CreateFunc(cloud, res.Path, parameters);

        return cmd;
    }

    private static ParamsData ParseLine(string param, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return ParamsData.Invalid;

        string pre = "/" + prefix;
        if (null == param || !param.Contains(pre)) return ParamsData.Invalid;

        int pos = param.LastIndexOf(pre, StringComparison.Ordinal);
        string path = WebDavPath.Clean(param.Substring(0, pos + 1));
        string data = param.Substring(pos + pre.Length);

        return new ParamsData
        {
            IsValid = true,
            Path = path,
            Data = data
        };
    }

    private struct ParamsData
    {
        public bool IsValid { get; set; }
        public string Path { get; set; }
        public string Data { get; set; }

        public static ParamsData Invalid => new() { IsValid = false };

    }

    private static SpecialCommandContainer FindCommandContainer(ICollection<string> parameters)
    {
        var commandContainer = CommandContainers
            .Where(cm =>
                cm.Commands.Length <= parameters.Count &&
                cm.Commands.SequenceEqual(parameters.Take(cm.Commands.Length)))
            .Aggregate((agg, next) => next.Commands.Length > agg.Commands.Length ? next : agg);

        return commandContainer;
    }


    private const string CommandRegexMask = @"((""((?<token>.*?)(?<!\\)"")|(?<token>[\S]+))(\s)*)";
#if NET7_0_OR_GREATER
    [GeneratedRegex(CommandRegexMask)]
    private static partial Regex CommandRegex();
    private static readonly Regex s_commandRegex = CommandRegex();
#else
    private static readonly Regex s_commandRegex = new(CommandRegexMask, RegexOptions.Compiled);
#endif

    private static List<string> ParseParameters(string paramString)
    {
        var list = s_commandRegex
            .Matches(paramString)
            // ReSharper disable once RedundantEnumerableCastCall
            .Cast<Match>()
            .Select(m => m.Groups["token"].Value)
            .ToList();

        return list;
    }

    private class SpecialCommandContainer
    {
        public string[] Commands;
        public Func<Cloud, string, IList<string>, SpecialCommand> CreateFunc;
    }
}
