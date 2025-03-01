#if WINDOWS || NET48

using System;
using System.ComponentModel;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace WinServiceInstaller;

internal class StubService : ServiceBase
{
    private Task _runner;

    protected override void OnStart(string[] args)
    {
        _runner = Task.Factory.StartNew(() =>
        {
            FireStart?.Invoke();
        });
    }

    protected override void OnStop()
    {
        FireStop?.Invoke();
    }

    // Attribute DesignerSerializationVisibility hides the error
    // 'Property XXX does not configure the code serialization for its property content
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action FireStart { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action FireStop { get; set; }
}

#endif
