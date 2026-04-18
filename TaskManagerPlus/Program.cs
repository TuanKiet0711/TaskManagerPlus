using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaskManagerPlus.Services;

namespace TaskManagerPlus
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Any(a => string.Equals(a, "--cleanup-today", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var db = new AppUsageDatabase();
                    int removed = db.DeleteTodaySessions();
                    Console.WriteLine("Deleted today's sessions: " + removed);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Cleanup failed: " + ex.Message);
                    Environment.ExitCode = 1;
                }

                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
