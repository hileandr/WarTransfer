using System;
using System.Collections.Generic;
using System.IO;
using WarTransfer;

namespace WarTransferCL
{
    public sealed class CommandLineErrorHandler : IWorkflowHandler
    {
        private readonly object gate = new object();
        private readonly TextWriter log;
        private readonly bool verbose;
        private volatile bool killed;
        private int current, total;
        public List<string> Errors { get; } = new List<string>();
        public CommandLineErrorHandler() : this(Console.Error, false) { }
        public CommandLineErrorHandler(TextWriter log, bool verbose) { this.log = log; this.verbose = verbose; }
        public bool KillProcess { get => killed; set { if (value) killed = true; } }
        public int CurrentStep { get => current; set { current = value; if (verbose) Log(LogType.Debug, $"Progress: {current}/{total}"); } }
        public int TotalSteps { get => total; set => total = value; }
        public void ReportError(string error, bool killProcess) { Log(LogType.Error, error); }
        public void ReportError(Exception error, bool killProcess) => ReportError(verbose ? error.ToString() : error.Message, killProcess);
        public void Log(LogType logType, string message)
        {
            lock (gate)
            {
                if (logType == LogType.Error) { Errors.Add(message); KillProcess = true; }
                if (logType != LogType.Debug || verbose) log.WriteLine($"[{logType}] {message}");
            }
        }
    }
}
