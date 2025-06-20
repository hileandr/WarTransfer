using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WarTransfer
{
    public enum LogType
    {
        Debug,
        UserInfo,
        Error
    }

    public interface IWorkflowHandler
    {
        int CurrentStep { get; set; }
        int TotalSteps { get; set; }
        bool KillProcess { get; set; }

        void ReportError(string error, bool killProcess);

        void ReportError(Exception error, bool killProcess);

        void Log(LogType logType, string message);
    }
}
