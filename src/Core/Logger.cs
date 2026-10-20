using System;
using System.IO;

namespace Ultron.Core
{
    public class LogEntry
    {
        public DateTime At { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }

        public LogEntry(string level, string message, string detail)
        {
            At = DateTime.Now;
            Level = level;
            Message = message;
            Detail = detail;
        }

        public string ToDisplayString()
        {
            string line = At.ToString("HH:mm:ss") + "  " + Level.PadRight(5) + "  " + Message;
            if (!string.IsNullOrWhiteSpace(Detail))
            {
                line += "  —  " + Detail;
            }
            return line;
        }
    }

    /// <summary>
    /// Event log only. Conversation text is deliberately not persisted.
    /// </summary>
    public class Logger
    {
        private readonly object _gate = new object();
        private readonly string _logPath;

        public event EventHandler<LogEntry> Logged;

        public Logger()
        {
            try
            {
                string directory = Path.Combine(AppSettings.SettingsDirectory, "logs");
                Directory.CreateDirectory(directory);
                _logPath = Path.Combine(directory, "ultron-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
            }
            catch
            {
                _logPath = null;
            }
        }

        public string LogPath
        {
            get { return _logPath; }
        }

        public void Info(string message, string detail)
        {
            Write("INFO", message, detail);
        }

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Warn(string message, string detail)
        {
            Write("WARN", message, detail);
        }

        public void Error(string message, string detail)
        {
            Write("ERROR", message, detail);
        }

        public void Error(string message, Exception exception)
        {
            string detail = exception == null ? null : exception.GetType().Name + ": " + exception.Message;
            Write("ERROR", message, detail);
        }

        private void Write(string level, string message, string detail)
        {
            string safeMessage = Redaction.Redact(message ?? string.Empty);
            string safeDetail = Redaction.Redact(detail ?? string.Empty);
            LogEntry entry = new LogEntry(level, safeMessage, safeDetail);

            lock (_gate)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_logPath))
                    {
                        File.AppendAllText(
                            _logPath,
                            entry.At.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " +
                            safeMessage + (string.IsNullOrEmpty(safeDetail) ? string.Empty : " | " + safeDetail) +
                            Environment.NewLine);
                    }
                }
                catch
                {
                    // Logging must never take the app down.
                }
            }

            EventHandler<LogEntry> handler = Logged;
            if (handler != null)
            {
                handler(this, entry);
            }
        }
    }
}
