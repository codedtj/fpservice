using System;
using System.Diagnostics;
using System.Text;

namespace FPService
{
    // Minimal logging adapter using System.Diagnostics TraceSource so no external package needed.
    // Can be redirected to files via Web.config <system.diagnostics> listeners if desired.
    internal static class Logger
    {
        private static readonly TraceSource Source = new TraceSource("FPService", SourceLevels.All);

        public static void Info(string message) => Write(TraceEventType.Information, message);
        public static void Debug(string message) => Write(TraceEventType.Verbose, message);
        public static void Error(string message, Exception ex) => Write(TraceEventType.Error, message + ": " + ex.Message + GetExceptionDetails(ex));

        public static void LogConnectionString(string connectionString)
        {
            try
            {
                // Try to redact password in standard MySQL connection string formats
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    Debug("Empty connection string");
                    return;
                }

                var redacted = connectionString;
                var tokens = connectionString.Split(';');
                for (int i = 0; i < tokens.Length; i++)
                {
                    var kv = tokens[i].Split(new[] { '=' }, 2);
                    if (kv.Length == 2)
                    {
                        var key = kv[0].Trim().ToLowerInvariant();
                        if (key == "password" || key == "pwd")
                        {
                            tokens[i] = kv[0] + "=****";
                        }
                    }
                }
                redacted = string.Join(";", tokens);
                Debug("ConnectionString=" + redacted);
            }
            catch (Exception ex)
            {
                Error("Failed to log connection string", ex);
            }
        }

        private static void Write(TraceEventType type, string message)
        {
            try
            {
                Source.TraceEvent(type, 0, message);
                Source.Flush();
            }
            catch
            {
                // Never throw from logger
            }
        }

        private static string GetExceptionDetails(Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine(ex.ToString());
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
