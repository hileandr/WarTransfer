using WarTransfer;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WarTransferUI;

namespace WarCopyUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppHandler handler = new AppHandler();

            Settings.LoadSettings(handler);

            if (handler.KillProcess)
            {
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppHandler handler = new AppHandler();

            Settings.SaveSettings(handler);

            base.OnExit(e);
        }
    }
}
