namespace ExxerAI.RealTimeCommunication.Extensions
{
    /// <summary>
    /// Rate-limited logging extensions to prevent log spam
    /// Improved from IndTrace with better thread safety and resource management
    /// </summary>
    public static class RateLimitedLoggingExtensions
    {
        private static readonly ConcurrentDictionary<string, DateTime> _lastLogTimes = new();
        private static readonly Timer _cleanupTimer;

        static RateLimitedLoggingExtensions()
        {
            // Clean up old entries every 5 minutes to prevent memory leaks
            _cleanupTimer = new Timer(CleanupOldEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Logs an information message only if the specified time interval has elapsed 
        /// since the last time the exact same message was logged
        /// </summary>
        /// <param name="logger">The ILogger instance</param>
        /// <param name="message">The message to log</param>
        /// <param name="minimumInterval">The minimum interval that must elapse before logging again</param>
        public static void LogInformationRateLimited(this ILogger logger, string message, TimeSpan minimumInterval)
        {
            LogRateLimited(logger, LogLevel.Information, message, minimumInterval);
        }

        /// <summary>
        /// Logs a warning message only if the specified time interval has elapsed 
        /// since the last time the exact same message was logged
        /// </summary>
        /// <param name="logger">The ILogger instance</param>
        /// <param name="message">The warning message to log</param>
        /// <param name="minimumInterval">The minimum interval that must elapse before logging again</param>
        public static void LogWarningRateLimited(this ILogger logger, string message, TimeSpan minimumInterval)
        {
            LogRateLimited(logger, LogLevel.Warning, message, minimumInterval);
        }

        /// <summary>
        /// Logs an error message only if the specified time interval has elapsed 
        /// since the last time the exact same message was logged
        /// </summary>
        /// <param name="logger">The ILogger instance</param>
        /// <param name="message">The error message to log</param>
        /// <param name="minimumInterval">The minimum interval that must elapse before logging again</param>
        public static void LogErrorRateLimited(this ILogger logger, string message, TimeSpan minimumInterval)
        {
            LogRateLimited(logger, LogLevel.Error, message, minimumInterval);
        }

        /// <summary>
        /// Logs a warning message with exception only if the specified time interval has elapsed 
        /// since the last time the exact same message was logged
        /// </summary>
        /// <param name="logger">The ILogger instance</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="message">The warning message to log</param>
        /// <param name="minimumInterval">The minimum interval that must elapse before logging again</param>
        public static void LogWarningRateLimited(this ILogger logger, Exception exception, string message, TimeSpan minimumInterval)
        {
            LogRateLimited(logger, LogLevel.Warning, exception, message, minimumInterval);
        }

        /// <summary>
        /// Logs an error message with exception only if the specified time interval has elapsed 
        /// since the last time the exact same message was logged
        /// </summary>
        /// <param name="logger">The ILogger instance</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="message">The error message to log</param>
        /// <param name="minimumInterval">The minimum interval that must elapse before logging again</param>
        public static void LogErrorRateLimited(this ILogger logger, Exception exception, string message, TimeSpan minimumInterval)
        {
            LogRateLimited(logger, LogLevel.Error, exception, message, minimumInterval);
        }

        /// <summary>
        /// Logs a message with template parameters only if the specified time interval has elapsed 
        /// since the last time the exact same message template was logged
        /// Uses default rate limit of 30 seconds for convenience
        /// </summary>
        /// <param name="logger">The ILogger instance</param>
        /// <param name="messageTemplate">The message template</param>
        /// <param name="args">Template arguments</param>
        public static void LogRateLimited(this ILogger logger, string messageTemplate, params object[] args)
        {
            LogRateLimited(logger, LogLevel.Information, messageTemplate, TimeSpan.FromSeconds(30), args);
        }

        /// <summary>
        /// Logs a message with template parameters only if the specified time interval has elapsed 
        /// since the last time the exact same message template was logged
        /// </summary>
        /// <param name="logger">The ILogger instance</param>
        /// <param name="logLevel">The log level</param>
        /// <param name="messageTemplate">The message template</param>
        /// <param name="minimumInterval">The minimum interval that must elapse before logging again</param>
        /// <param name="args">Template arguments</param>
        public static void LogRateLimited(this ILogger logger, LogLevel logLevel, string messageTemplate, TimeSpan minimumInterval, params object[] args)
        {
            if (ShouldLog(messageTemplate, minimumInterval))
            {
                logger.Log(logLevel, messageTemplate, args);
            }
        }

        private static void LogRateLimited(ILogger logger, LogLevel logLevel, string message, TimeSpan minimumInterval)
        {
            if (ShouldLog(message, minimumInterval))
            {
                logger.Log(logLevel, message);
            }
        }

        private static void LogRateLimited(ILogger logger, LogLevel logLevel, Exception exception, string message, TimeSpan minimumInterval)
        {
            if (ShouldLog(message, minimumInterval))
            {
                logger.Log(logLevel, exception, message);
            }
        }

        private static bool ShouldLog(string message, TimeSpan minimumInterval)
        {
            if (string.IsNullOrEmpty(message))
            {
                return true; // Always log null/empty messages (they should be rare)
            }

            var now = DateTime.UtcNow;
            var key = message;

            // Try to get or add the last log time for this message
            var lastLogTime = _lastLogTimes.AddOrUpdate(key, now, (_, existingTime) =>
            {
                // If enough time has passed, update to current time and allow logging
                if ((now - existingTime) >= minimumInterval)
                {
                    return now;
                }
                // Not enough time has passed, keep the existing time
                return existingTime;
            });

            // If the returned time equals the current time, it means we updated it and should log
            return lastLogTime == now;
        }

        private static void CleanupOldEntries(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-1); // Remove entries older than 1 hour
                var keysToRemove = new List<string>();

                foreach (var kvp in _lastLogTimes)
                {
                    if (kvp.Value < cutoffTime)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _lastLogTimes.TryRemove(key, out _);
                }
            }
            catch
            {
                // Swallow exceptions in cleanup to prevent crashes
                // This is a background cleanup operation and shouldn't affect the application
            }
        }
    }
}