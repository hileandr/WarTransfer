using System;
using WarTransfer;

namespace WarTransferCL
{
    public class CommandLineErrorHandler : IWorkflowHandler
    {
        public bool KillProcess
        {
            get
            {
                return false;
            }
            set
            {
                if (value)
                {
                    Environment.Exit(1);
                }
            }
        }

        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }

        public void ReportError(string error, bool killProcess)
        {
            Console.WriteLine(error);

            this.KillProcess = killProcess;
        }

        public void ReportError(Exception error, bool killProcess)
        {
            ReportError(error.Message, killProcess);
        }

        public void Log(string message)
        {
            Console.WriteLine(message);
        }

        public void Log(LogType logType, string message)
        {
            Console.WriteLine($"[{logType}]: {message}");
        }
    }
}
