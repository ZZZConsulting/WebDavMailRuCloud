using System;
using System.Collections.Generic;
using System.Text;

namespace YaR.Clouds.Base;

public static class WebDavPath
{
    public static bool IsFullPath(string path)
    {
        return path.StartsWith("/");
    }


    public static string Combine(string a, string b)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));

        if (b is null)
            return Clean(a, false);

        string right = Clean(b, false);

        if (a == "/disk")
            return b == Root ? "/disk" : "/disk" + right;

        if (a == Root)
            return right;

        string left = Clean(a, false);

        return left + right;
    }

    public static string Clean(string path, bool doAddFinalSeparator = false)
    {
        try
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            bool replaceSlash = path.Contains("\\") || path.Contains("//");
            bool insSlash = path.Length == 0 || path[0] != '/' && path[0] != '\\';
            bool addSlash = path.Length > 0 && doAddFinalSeparator && path[^1] != '/' && path[^1] != '\\';
            bool removeSlash = path.Length > 1 && (path[^1] == '/' || path[^1] == '\\');

            if ((replaceSlash ? 1 : 0) + (insSlash ? 1 : 0) + (addSlash ? 1 : 0) + (removeSlash ? 1 : 0) > 1)
            {
                StringBuilder text = new StringBuilder(path);
                text.Replace("\\", "/").Replace("//", "/");

                if (insSlash)
                    text.Insert(0, '/');
                if (addSlash)
                    text.Append('/');
                if (removeSlash)
                    text.Remove(text.Length - 1, 1);

                return text.ToString();
            }

            if (insSlash)
                return "/" + path;
            if (addSlash)
                return path + "/";
            if (removeSlash)
                return path.Remove(path.Length - 1, 1);

            return path;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public static string Parent(string path, string cmdPrefix = ">>")
    {
        // cause we use >> as a sign of special command
        int cmdPos = path.IndexOf(cmdPrefix, StringComparison.Ordinal);
        int len = path.EndsWith("/") ? path.Length - 1 : path.Length;
        if (len == 0)
            return Root;
        int slash = path.LastIndexOf("/", len - 1, cmdPos < 0 ? len : len - cmdPos, StringComparison.Ordinal);

        return slash > 0
            ? path.Substring(0, slash)
            : Root;
    }

    public static string Name(string path, string cmdPrefix = ">>")
    {
        // cause we use >> as a sign of special command
        int cmdPos = path.IndexOf(cmdPrefix, StringComparison.Ordinal);
        int len = path.EndsWith("/") ? path.Length - 1 : path.Length;
        if (len == 0)
            return "";
        int slash = path.LastIndexOf("/", len - 1, cmdPos < 0 ? len : len - cmdPos, StringComparison.Ordinal);

        if (slash < 0 && len == path.Length)
            return path;

        return path.Substring(slash + 1, len - slash - 1);
    }

    public static string Root => "/";
    public static string Separator => "/";

    public static bool IsParentOrSame(string parent, string child)
    {
        return IsParent(parent, child, true);
    }

    public static bool IsParent(string parent, string child, bool selfTrue = false, bool oneLevelDistanceOnly = false)
    {
        if (child.Equals(parent, StringComparison.InvariantCultureIgnoreCase))
            return selfTrue;

        if (parent.Length > child.Length)
            return false;

        if (parent == Root)
        {
            // Если родитель=root, то любой путь будет потомком, если не брать только ближайший уровень
            if (!oneLevelDistanceOnly)
                return true;
            // Если уровень только ближайший нужен,
            // то после части, совпадающей с родителем, не должно быть /,
            // исключение - / в виде последнего символа
            int slash = child.IndexOf(Separator, parent.Length, StringComparison.Ordinal);
            return slash < 0 || slash == child.Length - 1;
        }

        // Части потомка и родителя должны совпадать, и потомок должен быть длиннее
        if (parent.Length < child.Length &&
            child.StartsWith(parent, StringComparison.InvariantCultureIgnoreCase))
        {
            int start;
            // Если у родителя последний символ был /, то у потомка следующий символ не должен быть /
            if (child[parent.Length - 1] == '/' && child[parent.Length] != '/')
                start = parent.Length;
            else
                // Если у родителя последний символ был не /, то у потомка следующий символ должен быть /
                if (child[parent.Length - 1] != '/' && child[parent.Length] == '/')
                    start = parent.Length + 1;
                else
                {
                    // Тут какая-то ерунду с символами, это не родитель и потоком
                    return false;
                }
            int end = child[child.Length - 1] == '/' ? child.Length - 1 : child.Length;

            // Исключая возможный / в конце и исключая часть, совпадающую с родителем,
            // исключая / после родителя, от start, включая, до end, исключая (как для substring),
            // будет текст пути потомка.
            if (start >= end)
                return false;

            // Если уровень потомка не важен, то уже true.
            if (!oneLevelDistanceOnly)
                return true;

            // Если уровень потомка важен, то в интервале еще и символ / должен находиться.
            int slash = child.IndexOf(Separator, start, end - start, StringComparison.Ordinal);
            return slash < 0;
        }
        return false;
    }

    public static WebDavPathParts Parts(string path)
    {
        //TODO: refact
        var res = new WebDavPathParts
        {
            Parent = Parent(path),
            Name = Name(path)
        };

        return res;
    }

    public static List<string> GetParents(string path, bool includeSelf = true)
    {
        List<string> result = new List<string>();

        path = Clean(path);
        if (includeSelf)
            result.Add(path);

        while (path != Root)
        {
            path = Parent(path);
            result.Add(path);
        }

        return result;
    }

    public static string ModifyParent(string path, string oldParent, string newParent)
    {
        if (!IsParentOrSame(oldParent, path))
            return path;

        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (oldParent is null)
            throw new ArgumentNullException(nameof(oldParent));
        if (newParent is null)
            throw new ArgumentNullException(nameof(newParent));
        if (path.Length < oldParent.Length)
            throw new ArgumentException($"Value of {nameof(oldParent)} is longer then length of {nameof(path)}");

        string pathTmp = Clean(path, true);
        string oldParentTmp = Clean(oldParent, true);
        path = pathTmp.Remove(0, oldParentTmp.Length).ToString();

        return Combine(newParent, path);
    }

    public static bool PathEquals(string path1, string path2)
    {
        return Clean(path1).Equals(Clean(path2), StringComparison.InvariantCultureIgnoreCase);
    }

    public static string EscapeDataString(string path)
    {
        return Uri
            .EscapeDataString(path ?? string.Empty)
            .Replace("#", "%23");
    }
}

public struct WebDavPathParts
{
    public string Parent { get; set; }
    public string Name { get; set; }
}
