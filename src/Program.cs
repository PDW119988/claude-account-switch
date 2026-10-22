using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Claude Account Switch")]
[assembly: System.Reflection.AssemblyProduct("Claude Account Switch")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace ClaudeAccountSwitch
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool trayOnly = args.Any(a => a == "--tray");

            bool first;
            using (var mutex = new Mutex(true, @"Local\ClaudeAccountSwitch", out first))
            using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ClaudeAccountSwitch.Show"))
            {
                if (!first)
                {
                    if (!trayOnly) show.Set();
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp(!trayOnly, show));
            }
        }
    }
}
