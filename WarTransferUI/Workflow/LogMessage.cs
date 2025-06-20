using WarTransfer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WarTransferUI
{
    public class LogMessage
    {
        public LogType LogType { get; set; }
        public string Message { get; set; }

        public Brush MessageColor
        {
            get
            {
                switch (LogType)
                {
                    case LogType.Error: return new SolidColorBrush(Color.FromRgb(255, 0, 0));
                    case LogType.Debug: return new SolidColorBrush(Color.FromRgb(255, 255, 0));
                    default: return new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }
            }
        }

        internal LogMessage(LogType logType, string message)
        {
            LogType = logType;
            Message = message;
        }
    }
}
