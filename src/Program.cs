using System;
using System.Threading;
using System.Windows.Forms;

namespace DeadSpaceTextureLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (IntegrationHelper.TryRun(args)) return;
            bool created;
            using (Mutex singleInstance = new Mutex(true, "Local\\Rama2120.DeadSpaceTextureLauncher", out created))
            {
                if (!created)
                {
                    MessageBox.Show("The texture launcher is already running.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    Log.Start();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                    {
                        Log.Error(e.Exception.ToString());
                        MessageBox.Show(e.Exception.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    };
                    AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                    {
                        Log.Error(Convert.ToString(e.ExceptionObject));
                    };

                    LaunchInvocation invocation = LaunchInvocation.Parse(args);
                    Application.Run(new MainForm(invocation));
                }
                catch (Exception ex)
                {
                    try { Log.Error(ex.ToString()); } catch { }
                    if (!AppInfo.TestMode)
                        MessageBox.Show("The launcher could not start:\n\n" + ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
