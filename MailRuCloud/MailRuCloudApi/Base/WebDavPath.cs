﻿using System;
using System.Collections.Generic;

namespace YaR.Clouds.Base
{
    public static class WebDavPath
    {
        public static bool IsFullPath(string path)
        {
            return path.StartsWith("/");
        }


        public static string Combine(string a, string b)
        {
            a = Clean(a);
            b = Clean(b);
            a = a.Trim('/');
            b = b.TrimStart('/');
            string res = a + (string.IsNullOrEmpty(b) ? "" : "/" + b);
            if (!res.StartsWith("/")) res = "/" + res;
            return res;

        }

        public static string Clean(string path, bool doAddFinalSeparator = false)
        {
            try
            {
                string res = path?.Replace("\\", "/").Replace("//", "/")
                    ?? throw new ArgumentNullException(nameof(path));
                if (res.Length > 1 && !doAddFinalSeparator)                
                    return res.TrimEnd('/');
                if (doAddFinalSeparator && !res.EndsWith("/"))
                    res += Separator;
                return res;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }

        public static string Parent(string path, string cmdPrefix = ">>")
        {
            //TODO: refact
            path = path.TrimEnd('/');

            // cause we use >> as a sign of special command
            int cmdPos = path.IndexOf(cmdPrefix, StringComparison.Ordinal);

            int pos = cmdPos > 0
                ? path.LastIndexOf("/", 0, cmdPos + 1, StringComparison.Ordinal)
                : path.LastIndexOf("/", StringComparison.Ordinal);

            return pos > 0
                ? path.Substring(0, pos)
                : "/";
        }

        public static string Name(string path, string cmdPrefix = ">>")
        {
            //TODO: refact
            path = path.TrimEnd('/');

            // cause we use >> as a sign of special command
            int cmdPos = path.IndexOf(cmdPrefix, StringComparison.Ordinal);

            int pos = cmdPos > 0
                    ? path.LastIndexOf("/", 0, cmdPos + 1, StringComparison.Ordinal)
                    : path.LastIndexOf("/", StringComparison.Ordinal);

            string res = path.Substring(pos+1);
            return res;
        }

        public static string Root => "/";
        public static string Separator => "/";

        public static bool IsParentOrSame(string parent, string child)
        {
            return IsParent(parent, child, true);
        }

        public static bool IsParent(string parent, string child, bool selfTrue = false)
        {
            parent = Clean(parent, true);
            child = Clean(child, true);
            return child.StartsWith(parent) && (selfTrue || parent.Length != child.Length);
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

            path = Clean(path, true);
            oldParent = Clean(oldParent, true);

            path = path.Remove(0, oldParent.Length);

            return Combine(newParent, path);
        }

        public static bool PathEquals(string path1, string path2)
        {
            return string.Compare(Clean(path1), Clean(path2), StringComparison.InvariantCultureIgnoreCase) == 0;
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
}
