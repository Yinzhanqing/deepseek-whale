using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DeepSeekWhale
{
    public class Cli
    {
        public bool SelfTest;
        public bool Render;
        public bool AutoStart;
        public bool TestMode;
        public bool NoDsh;
        public string RenderDir = "render";
        public string DataDir;
        public string ApiBase;
        public string Key;
        public int ExitAfterMs;
        public string SaveFrame;
        public bool Expanded;
        public string Shot;
        public string IconOut;
        public bool DemoWater;
        public bool DemoWalk;
        public bool UsageTab;
        public bool DemoCannon;
        public bool DemoTarget;
        public bool DemoMenu;

        public static Cli Parse(string[] a)
        {
            var c = new Cli();
            for (int i = 0; i < a.Length; i++)
            {
                string k = a[i];
                string v = i + 1 < a.Length ? a[i + 1] : null;
                switch (k)
                {
                    case "--selftest": c.SelfTest = true; break;
                    case "--render": c.Render = true; if (v != null && !v.StartsWith("--")) { c.RenderDir = v; i++; } break;
                    case "--autostart": c.AutoStart = true; break;
                    case "--test": c.TestMode = true; break;
                    case "--no-dsh": c.NoDsh = true; break;
                    case "--data-dir": c.DataDir = v; i++; break;
                    case "--api-base": c.ApiBase = v; i++; break;
                    case "--key": c.Key = v; i++; break;
                    case "--save-frame": c.SaveFrame = v; i++; break;
                    case "--expanded": c.Expanded = true; break;
                    case "--shot": c.Shot = v; i++; break;
                    case "--icon": c.IconOut = v; i++; break;
                    case "--demo-water": c.DemoWater = true; break;
                    case "--demo-walk": c.DemoWalk = true; break;
                    case "--usage-tab": c.UsageTab = true; break;
                    case "--demo-cannon": c.DemoCannon = true; break;
                    case "--demo-target": c.DemoTarget = true; break;
                    case "--demo-menu": c.DemoMenu = true; break;
                    case "--exit-after": c.ExitAfterMs = int.Parse(v); i++; break;
                }
            }
            return c;
        }
    }

    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var cli = Cli.Parse(args);
            if (cli.SelfTest || cli.Render || !string.IsNullOrEmpty(cli.IconOut))
            {
                int rc = 2;
                try
                {
                    if (cli.SelfTest) rc = SelfTest.Run();
                    else if (cli.Render) rc = DevRender.Run(cli.RenderDir);
                    else rc = DevRender.RenderIcon(cli.IconOut);
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devmode-crash.txt"), ex.ToString()); } catch { }
                }
                Environment.Exit(rc);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                Log.Error("thread exception: " + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Log.Error("unhandled: " + e.ExceptionObject);
            };

            bool created;
            using (var mutex = new Mutex(true, @"Local\DeepSeekWhale.Mutex", out created))
            {
                if (!created)
                {
                    try
                    {
                        using (var ev = EventWaitHandle.OpenExisting(@"Local\DeepSeekWhale.Show")) ev.Set();
                    }
                    catch { }
                    return 0;
                }
                using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\DeepSeekWhale.Show"))
                {
                    var app = new TrayApp(cli);
                    Application.Run(app);
                    GC.KeepAlive(mutex);
                }
            }
            return 0;
        }
    }
}
