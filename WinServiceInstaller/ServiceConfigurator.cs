using System;
using System.Reflection;

#if WINDOWS || NET48
#region WINDOWS || NET48

#if !NET48

using System.Text;
using System.Diagnostics;
using System.Security;

#endif

using System.ServiceProcess;
using Microsoft.Win32;

namespace WinServiceInstaller;

public class ServiceConfigurator
{
    private string _name;
    private string _displayName;
    private string _description;
    public Assembly Assembly { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            MyServiceInstaller.ServiceName = value;
            _name = value;
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            MyServiceInstaller.DisplayName = value;
            _displayName = value;
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            MyServiceInstaller.Description = value;
            _description = value;
        }
    }

    public string CommandLine { get; set; }

    public Action FireStart { get; set; }
    public Action FireStop { get; set; }

    public void Install()
    {
#if NET48
        System.Configuration.Install.ManagedInstallerClass.InstallHelper([Assembly.GetExecutingAssembly().Location]);

        string cmd = Assembly.Location;
        if (!string.IsNullOrWhiteSpace(CommandLine))
            cmd = string.Concat(cmd, " ", CommandLine);
        SetCommandLine(cmd);
#else
        string exePath = GetExePath();
        var serviceKey = GetRegistryKey();

        // Если ключа реестра нет, значит сначала надо программу прописать сервисом, а только потом править параметры запуска
        if (serviceKey == null)
        {
            string consoleText = RunSc("create", _name,
                "start=", "auto",
                "binPath=", exePath,
                "DisplayName=", _displayName);
            if (!(consoleText.Contains("успех", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("success", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("PENDING", StringComparison.OrdinalIgnoreCase)
                ))
                throw new Exception("Error while installing the service\r\n" + consoleText);
        }
        else
        {
            // Error codes:
            // https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--1000-1299-

            // На случай, если сервис уже установлен и запущен, пытаемся его остановить
            string consoleText = RunSc("stop", _name);

            if (!(consoleText.Contains("успех", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("success", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("PENDING", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("1060:" /*ERROR_SERVICE_DOES_NOT_EXIST*/, StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("1062:" /*ERROR_SERVICE_NOT_ACTIVE*/, StringComparison.OrdinalIgnoreCase)
                ))
                throw new Exception("Error while stopping the service\r\n" + consoleText);
        }
        {
            string consoleText = RunSc("Description", _name, _description);
            if (!(consoleText.Contains("успех", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("success", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("PENDING", StringComparison.OrdinalIgnoreCase)
                ))
                throw new Exception("Error while installing the service\r\n" + consoleText);
        }
        {
            // В случае сбоя сервиса, система его сразу автоматически перезапустит
            string consoleText = RunSc("failure", _name, "reset=", "0", "actions=", "restart/0");
            if (!(consoleText.Contains("успех", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("success", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("PENDING", StringComparison.OrdinalIgnoreCase)
                ))
                throw new Exception("Error while installing the service\r\n" + consoleText);
        }

        string cmd = string.IsNullOrWhiteSpace(CommandLine)
            ? exePath
            : string.Concat(exePath, " ", CommandLine);
        SetCommandLine(cmd);

        int counter = 120;
        while (counter-- > 0 && NeedWaitSc(_name))
            System.Threading.Thread.Sleep(500);


        {
            // Запуск сервиса
            string consoleText = RunSc("start", _name);

            if (!(consoleText.Contains("успех", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("success", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("PENDING", StringComparison.OrdinalIgnoreCase)
                ))
                throw new Exception("Error while starting the service\r\n" + consoleText);
        }

        Console.WriteLine("Well done!");
#endif
    }

    public void Run()
    {
        var service = new StubService
        {
            ServiceName = "wdmrc",
            FireStart = FireStart,
            FireStop = FireStop
        };

        ServiceBase.Run([service]);
    }

    public void Uninstall()
    {
#if NET48
        string cmd = Assembly.GetExecutingAssembly().Location;
        SetCommandLine(cmd);
        System.Configuration.Install.ManagedInstallerClass.InstallHelper(["/u", cmd]);
#else
        string exePath = GetExePath();
        var serviceKey = GetRegistryKey();

        {
            // In case the service is running let's stop it
            string consoleText = RunSc("stop", _name);
            if (!(consoleText.Contains("успех", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("success", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("PENDING", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("1060:" /*ERROR_SERVICE_DOES_NOT_EXIST*/, StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("1062:" /*ERROR_SERVICE_NOT_ACTIVE*/, StringComparison.OrdinalIgnoreCase)
                ))
                throw new Exception("Error while deleting the service\r\n" + consoleText);
        }

        int counter = 120;
        while (counter-- > 0 && NeedWaitSc(_name))
            System.Threading.Thread.Sleep(500);

        {
            // Then let's delete it
            string consoleText = RunSc("delete", _name);
            if (!(consoleText.Contains("успех", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("success", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("PENDING", StringComparison.OrdinalIgnoreCase)
                || consoleText.Contains("1060:" /*ERROR_SERVICE_DOES_NOT_EXIST*/, StringComparison.OrdinalIgnoreCase)
                ))
                throw new Exception("Error while deleting the service");
        }

        Console.WriteLine("Well done!");
#endif
    }


    private void SetCommandLine(string cmd)
    {
        var serviceKey = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{Name}", true)
            ?? throw new Exception($"Could not locate Registry Key for the service '{Name}'");

        serviceKey.SetValue("ImagePath", cmd, RegistryValueKind.String);
    }

#if !NET48

    private static bool NeedWaitSc(string serviceName)
    {
        return RunSc("query", serviceName).Contains("PENDING", StringComparison.OrdinalIgnoreCase);
    }

    private static string RunSc(params string[] args)
    {
        StringBuilder sb = new StringBuilder();
        foreach (var item in args)
        {
            sb.Append('"');
            sb.Append(item);
            sb.Append('"');
            sb.Append(' ');
        }

        ProcessStartInfo p = new ProcessStartInfo();
        p.FileName = System.IO.Path.Combine(Environment.GetEnvironmentVariable("SystemRoot"), "system32", "sc.exe");
        p.Arguments = sb.ToString();
        p.UseShellExecute = false;
        p.CreateNoWindow = true;
        p.WindowStyle = ProcessWindowStyle.Hidden;
        p.RedirectStandardOutput = true;
        p.RedirectStandardError = true;
        //p.StandardOutputEncoding = Encoding.UTF8;
        //p.StandardErrorEncoding = Encoding.UTF8;

        Process proc = new Process();
        proc.StartInfo = p;
        proc.Start();

        string standardOutput = proc.StandardOutput.ReadToEnd();
        string standardError = proc.StandardError.ReadToEnd();

        proc.WaitForExit(20 * 1000 /* 20 seconds */ );
        if (!proc.HasExited)
        {
            proc.Kill();
        }
        //if (proc.ExitCode != 0)
        //{
        //    Console.WriteLine(sb.ToString());
        //}
        return standardOutput + standardError;
    }

    private string GetExePath()
    {
        // Так как нет простого нормального способа получить доступ к EXE,
        // берется путь к DLL сборки и расширение DLL заменяется на EXE
        return Assembly.Location.Replace(".dll", ".exe");
    }

    private RegistryKey GetRegistryKey()
    {
        try
        {
            // At first let's check for enough rights to write into registry
            // Or we need to re-run the program like "Run As Administrator".
            _ = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services", true);
        }
        catch (SecurityException)
        {
            throw new Exception(
                "Not enough rights to complete the operation.\r\n" +
                "Please use 'Run As Administrator' to run the program.");
        }

        return Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{Name}", true);
    }

#endif
}

#endregion WINDOWS || NET48
#else
#region Non-Windows platforms

namespace WinServiceInstaller;

public class ServiceConfigurator
{
    public Assembly Assembly { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string CommandLine { get; set; }

    public Action FireStart { get; set; }
    public Action FireStop { get; set; }

#pragma warning disable CA1822 // Mark members as static

    public void Install() => Nope();
    public void Run() => Nope();
    public void Uninstall() => Nope();

    public void Nope()
    {
        Console.WriteLine("Nope! Please use net#.0-windows platform version of the program!");
    }

#pragma warning restore CA1822 // Mark members as static
}

#endregion Non-Windows platforms
#endif
