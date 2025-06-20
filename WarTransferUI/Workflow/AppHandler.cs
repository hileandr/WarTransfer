using WarTransfer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WarTransferUI
{
    internal class AppHandler : IWorkflowHandler
    {
        internal static AppHandler Instance = new AppHandler();

        public bool KillProcess { get; set; }
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }

        public void Log(LogType logType, string message)
        {
            MessageBox.Show(message, "Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void ReportError(string error, bool killProcess)
        {
            MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            KillProcess |= killProcess;
        }

        public void ReportError(Exception error, bool killProcess)
        {
            ReportError("An unexpected error occured: " + error.Message, killProcess);
        }
    }
}
