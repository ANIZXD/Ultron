using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Ultron
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                // Render at physical pixels so the fixed 1180x760 layout is not
                // virtualized/cropped by Windows display scaling.
                SetProcessDPIAware();
            }
            catch
            {
                // Older Windows or a locked-down host: continue at system DPI.
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool smoke = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--smoke", StringComparison.OrdinalIgnoreCase))
                {
                    smoke = true;
                }
            }

            Application.Run(new MainForm(smoke));
        }
    }
}
