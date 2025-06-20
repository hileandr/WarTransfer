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
    internal class UIWorkflowHandler : IWorkflowHandler
    {
        private object m_Lock = new object();
        private bool m_KillProcess = false;
        private int m_CurrentStep = 0;
        private int m_TotalSteps = 0;
        private Action<LogMessage>? m_AddLogAction = null;
        private Action<int, int>? m_ReportProgressAction = null;

        public bool KillProcess
        {
            get
            {
                lock (m_Lock)
                {
                    return m_KillProcess;
                }
            }
            set
            {
                lock (m_Lock)
                {
                    m_KillProcess = value;
                }
            }
        }

        public int CurrentStep
        {
            get
            {
                lock (m_Lock)
                {
                    return m_CurrentStep;
                }
            }
            set
            {
                lock (m_Lock)
                {
                    m_CurrentStep = value;

                    if (!m_KillProcess)
                    {
                        m_ReportProgressAction?.Invoke(m_CurrentStep, m_TotalSteps);
                    }
                }
            }
        }

        public int TotalSteps
        {
            get
            {
                lock (m_Lock)
                {
                    return m_TotalSteps;
                }
            }
            set
            {
                lock (m_Lock)
                {
                    m_TotalSteps = value;

                    if (!m_KillProcess)
                    {
                        m_ReportProgressAction?.Invoke(m_CurrentStep, m_TotalSteps);
                    }
                }
            }
        }

        public void Init(Action<LogMessage> addLog, Action<int, int> reportProgress)
        {
            m_KillProcess = false;
            m_CurrentStep = 0;
            m_TotalSteps = 0;

            m_AddLogAction = addLog;
            m_ReportProgressAction = reportProgress;
        }

        public void Log(LogType logType, string message)
        {
#if DEBUG
            m_AddLogAction?.Invoke(new LogMessage(logType, message));
#else
            if (logType != LogType.Debug)
            {
                m_AddLogAction?.Invoke(new LogMessage(logType, message));
            }
#endif
        }

        public void ReportError(string error, bool killProcess)
        {
            Log(LogType.Error, "[ERROR] " + error);
            this.KillProcess |= killProcess;
        }

        public void ReportError(Exception error, bool killProcess)
        {
            ReportError("An unknown error occured: " + error.Message, killProcess);
        }
    }
}
