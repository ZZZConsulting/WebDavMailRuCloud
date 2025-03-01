#if WINDOWS || NET48

using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace WinServiceInstaller
{
    [RunInstaller(true)]
    public class MyServiceInstaller : Installer
    {
        public MyServiceInstaller()
        {
            var processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem
            };

            var serviceInstaller = new ServiceInstaller
            {
                StartType = ServiceStartMode.Automatic,
                ServiceName = ServiceName,
                DisplayName = DisplayName,
                Description = Description
            };

            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }

        // Attribute DesignerSerializationVisibility hides the error
        // 'Property XXX does not configure the code serialization for its property content
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static string ServiceName { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static string DisplayName { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static string Description { get; set; }
    }
}

#endif
