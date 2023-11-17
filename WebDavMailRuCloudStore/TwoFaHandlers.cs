﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace YaR.Clouds.WebDavStore
{
    public class TwoFactorAuthHandlerInfo
    {
        public string Name { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Parameters { get; set; }
    }

    public static class TwoFaHandlers
    {
        private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(TwoFaHandlers));

        static TwoFaHandlers()
        {
            HandlerTypes = GetHandlers().ToList();
        }

        private static readonly List<Type> HandlerTypes;


        public static ITwoFaHandler Get(TwoFactorAuthHandlerInfo handlerInfo)
        {
            var type = HandlerTypes.FirstOrDefault(t => t.Name == handlerInfo.Name);
            if (null == type) return null;

            ITwoFaHandler inst = null;
            try
            {
                inst = (ITwoFaHandler)Activator.CreateInstance(type, handlerInfo.Parameters);
            }
            catch (Exception e)
            {
                Logger.Error($"Cannot create instance of 2FA handler {handlerInfo.Name}. {e}");
            }

            return inst;
        }

        private static IEnumerable<Type> GetHandlers()
        {
            var files = Directory.EnumerateFiles(
                Path.GetDirectoryName(typeof(TwoFaHandlers).Assembly.Location) ?? throw new InvalidOperationException(),
                "MailRuCloud.TwoFA*.dll",
                SearchOption.TopDirectoryOnly);

            var types = new List<Type>();
            foreach (var file in files)
            {
                try
                {
                    //If an application has been copied from the web, it is flagged by Windows as being a web application, even if it resides on the local computer.
                    //You can change that designation by changing the file properties, or you can use the element to grant the assembly full trust.
                    //As an alternative, you can use the UnsafeLoadFrom method to load a local assembly that the operating system has flagged as having been loaded from the web.
                    Assembly assembly = Assembly.UnsafeLoadFrom(file);

                    types.AddRange(assembly.ExportedTypes.Where(type => type.GetInterfaces().Contains(typeof(ITwoFaHandler))));
                }
                catch (Exception e)
                {
                    Logger.Error($"Cannot load 2FA assembly {file}. {e}");
                }
            }

            return types;
        }
    }
}
