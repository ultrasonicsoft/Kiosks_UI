using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows;

namespace Kiosk
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //connection string for SQL
        public static string viadatConnString = "";//"Data Source=" + this.Properties["IPADDRESS"] + ";Initial Catalog=criterion01;User=viadat;Password=viadat1";
        //@"server=localhost;database=dbcriterion01;Trusted_Connection=yes;User=viadat;Password=viadat1";
        //@"Data Source=10.1.1.147;Initial Catalog=criterion01;User=viadat;Password=viadat1";

        public const bool DEBUG = false;

        public static EventLog AppEventLog;

        private string parseID(string arg)
        {
            uint n;
            if (arg.Length == 4 && UInt32.TryParse(arg, out n))
            {
                return arg;
            }
            else
            {
                throw new InvalidOperationException("Invalid kioskid from command line : |"+arg+"|");
            }
        }

        private string parseIP(string arg)
        {
            IPAddress n;
            if (IPAddress.TryParse(arg, out n))
            {
                return arg;
            }
            else
            {
                throw new InvalidOperationException("Invalid IP Address from command line : |"+arg+"|");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args == null || e.Args.Count() != 2 )
            {
                throw new InvalidOperationException("Invalid command line parameters: Usage: Kiosk.exe <kioskid> <ipaddress>");
            }
            else if (e.Args.Count() == 2)
            {
                this.Properties["KIOSKID"] = parseID(e.Args[0]);
                this.Properties["IPADDRESS"] = parseIP(e.Args[1]);
            }
            else
            {
                throw new InvalidOperationException("Invalid command line parameters: (" + e.Args.Count() + ") " + e.Args.ToString());
            }

            base.OnStartup(e);
            
            bool restart = false;
            DateTime lastRestart = DateTime.Today;
            do
            {
                try
                {
                    restart = false;
                    var mainWnd = new MainWindow();
                    mainWnd.ShowDialog();
                    GC.Collect();
                }
                catch (Exception ex)
                {
                    AppEventLog.WriteEntry(ex.ToString());
                }
            } while (restart);
        }
    }
}
