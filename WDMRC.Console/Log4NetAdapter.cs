using System;
using NWebDav.Server.Logging;

namespace YaR.Clouds.Console
{
    public class Log4NetAdapter : ILoggerFactory
    {
        private class Log4NetLoggerAdapter : ILogger
        {
            private readonly log4net.ILog _log;

            public Log4NetLoggerAdapter(Type type)
            {
                // Obtain the Log4NET logger for this type
                _log = log4net.LogManager.GetLogger(type);
            }

            public bool IsLogEnabled(LogLevel logLevel)
            {
                return logLevel switch
                {
                    LogLevel.Debug => _log.IsDebugEnabled,
                    LogLevel.Info => _log.IsInfoEnabled,
                    LogLevel.Warning => _log.IsWarnEnabled,
                    LogLevel.Error => _log.IsErrorEnabled,
                    LogLevel.Fatal => _log.IsFatalEnabled,
                    _ => throw new ArgumentException($"Log level '{logLevel}' is not supported.", nameof(logLevel))
                };
            }

            public void Log(LogLevel logLevel, Func<string> messageFunc, Exception exception)
            {
                string msg;
                try
                {
                    msg = messageFunc();
                }
                catch (Exception e)
                {
                    msg = "Failed to get error message: " + e.Message;
                }

                Log(logLevel, msg, exception);
            }

            public void Log(LogLevel logLevel, string message, Exception exception = null)
            {
                switch (logLevel)
                {
                    case LogLevel.Debug:
                        //if (_log.IsDebugEnabled)
                        //    _log.Debug(msg, exception);
                        break;
                    case LogLevel.Info:
                        if (_log.IsInfoEnabled)
                            _log.Info(message, exception);
                        break;
                    case LogLevel.Warning:
                        if (_log.IsWarnEnabled)
                            _log.Warn(message, exception);
                        break;
                    case LogLevel.Error:
                        if (_log.IsErrorEnabled)
                            _log.Error(message, exception);
                        break;
                    case LogLevel.Fatal:
                        if (_log.IsFatalEnabled)
                            _log.Fatal(message, exception);
                        break;
                    default:
                        throw new ArgumentException($"Log level '{logLevel}' is not supported.", nameof(logLevel));
                }
            }
        }

        public ILogger CreateLogger(Type type)
        {
            return new Log4NetLoggerAdapter(type);
        }
    }
}