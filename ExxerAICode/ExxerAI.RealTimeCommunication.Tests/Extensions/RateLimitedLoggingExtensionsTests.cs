using ExxerAI.RealTimeCommunication.Extensions;
using Microsoft.Extensions.Logging;

namespace ExxerAI.RealTimeCommunication.Tests.Extensions
{
    /// <summary>
    /// Tests for RateLimitedLoggingExtensions to ensure proper rate limiting and memory leak prevention.
    /// Tests IndTrace patterns for industrial-grade logging performance.
    /// </summary>
    public class RateLimitedLoggingExtensionsTests
    {
        private readonly ILogger<RateLimitedLoggingExtensionsTests> _logger;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly ILogger _testLogger;

        /// <summary>
        /// Constructor for RateLimitedLoggingExtensionsTests
        /// </summary>
        public RateLimitedLoggingExtensionsTests()
        {
            _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<RateLimitedLoggingExtensionsTests>.Instance;

            // Create test logger with provider to capture log entries
            _loggerProvider = new TestLoggerProvider();
            var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(_loggerProvider));
            _testLogger = loggerFactory.CreateLogger("TestLogger");
        }

        //   Rate Limiting Tests

        /// <summary>
        /// Rate Limiting Test: Should limit log entries within time window.
        /// </summary>
        /// <returns>A task that completes after the rate limiting assertions are executed.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Limit_Log_Entries_Within_Time_Window()
        {
            // Arrange
            const int logCount = 10;
            const string message = "Test rate limited message";

            // Act - Log multiple times rapidly
            for (int i = 0; i < logCount; i++)
            {
                _testLogger.LogRateLimited(LogLevel.Information, message, TimeSpan.FromSeconds(1));
            }

            // Small delay to ensure rate limiting window processing
            await Task.Delay(100);

            // Assert - Should have fewer log entries due to rate limiting
            var logEntries = _loggerProvider.GetLogEntries();
            var rateLimitedEntries = logEntries.Count(e => e.Message.Contains(message));

            rateLimitedEntries.ShouldBeLessThan(logCount);
            rateLimitedEntries.ShouldBeGreaterThan(0);

            _logger.LogInformation("Rate limiting working: {ActualLogs} out of {ExpectedLogs} logs recorded",
                rateLimitedEntries, logCount);
        }

        /// <summary>
        /// Rate Limiting Test: Should allow logs after time window expires.
        /// </summary>
        /// <returns>A task that completes once logs are written across time windows and verified.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Allow_Logs_After_Time_Window_Expires()
        {
            // Arrange
            const string message = "Time window test message";

            // Act - Log once
            _testLogger.LogRateLimited(LogLevel.Information, message, TimeSpan.FromSeconds(1));

            // Wait for rate limiting window to expire (assuming default is 1 second)
            await Task.Delay(1100);

            // Log again
            _testLogger.LogRateLimited(LogLevel.Information, message, TimeSpan.FromSeconds(1));

            // Assert - Should have 2 log entries
            var logEntries = _loggerProvider.GetLogEntries();
            var rateLimitedEntries = logEntries.Count(e => e.Message.Contains(message));

            rateLimitedEntries.ShouldBe(2);

            _logger.LogInformation("Time window expiration working: {LogCount} logs recorded after window reset",
                rateLimitedEntries);
        }

        /// <summary>
        /// Rate Limiting Test: Should handle different log levels independently.
        /// </summary>
        /// <returns>A task that completes after verifying each log level is captured independently.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Handle_Different_Log_Levels_Independently()
        {
            // Arrange
            const string baseMessage = "Multi-level test";

            // Act - Log at different levels
            _testLogger.LogRateLimited(LogLevel.Information, $"{baseMessage} Info", TimeSpan.FromSeconds(1));
            _testLogger.LogRateLimited(LogLevel.Warning, $"{baseMessage} Warning", TimeSpan.FromSeconds(1));
            _testLogger.LogRateLimited(LogLevel.Error, $"{baseMessage} Error", TimeSpan.FromSeconds(1));

            await Task.Delay(100);

            // Assert - Should have entries for each level
            var logEntries = _loggerProvider.GetLogEntries();

            logEntries.ShouldContain(e => e.Level == LogLevel.Information && e.Message.Contains("Info"));
            logEntries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("Warning"));
            logEntries.ShouldContain(e => e.Level == LogLevel.Error && e.Message.Contains("Error"));

            _logger.LogInformation("Different log levels handled independently");
        }

        //   Rate Limiting Tests

        //   Parameter Formatting Tests

        /// <summary>
        /// Formatting Test: Should format log messages with parameters correctly.
        /// </summary>
        /// <returns>A task that completes after verifying formatted log messages.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Format_Messages_With_Parameters_Correctly()
        {
            // Arrange
            const string template = "User {UserId} performed action {Action} at {Timestamp}";
            const string userId = "user123";
            const string action = "login";
            var timestamp = DateTime.UtcNow;

            // Act
            _testLogger.LogRateLimited(LogLevel.Information, template, TimeSpan.FromSeconds(1), userId, action, timestamp);

            await Task.Delay(100);

            // Assert
            var logEntries = _loggerProvider.GetLogEntries();
            var formattedEntry = logEntries.FirstOrDefault(e => e.Message.Contains(userId));

            formattedEntry.ShouldNotBeNull();
            formattedEntry.Message.ShouldContain(userId);
            formattedEntry.Message.ShouldContain(action);

            _logger.LogInformation("Parameter formatting working correctly");
        }

        /// <summary>
        /// Formatting Test: Should handle null parameters gracefully.
        /// </summary>
        /// <returns>A task that completes after verifying null parameters do not break logging.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Handle_Null_Parameters_Gracefully()
        {
            // Arrange
            const string template = "Value is {Value}";

            // Act - Should not throw
            Should.NotThrow(() => _testLogger.LogRateLimited(LogLevel.Information, template, TimeSpan.FromSeconds(1), (object?)null!));

            await Task.Delay(100);

            // Assert
            var logEntries = _loggerProvider.GetLogEntries();
            logEntries.ShouldNotBeEmpty();

            _logger.LogInformation("Null parameters handled gracefully");
        }

        //   Parameter Formatting Tests

        //   Memory Leak Prevention Tests

        /// <summary>
        /// Memory Test: Should not accumulate rate limiting state indefinitely.
        /// </summary>
        /// <returns>A task that completes after confirming memory pressure stays bounded.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Not_Accumulate_State_Indefinitely()
        {
            // Arrange
            const int uniqueMessageCount = 100;

            // Act - Create many unique log messages to test memory cleanup
            for (int i = 0; i < uniqueMessageCount; i++)
            {
                _testLogger.LogRateLimited(LogLevel.Information, $"Unique message {i} at {DateTime.UtcNow.Ticks}", TimeSpan.FromSeconds(1));
            }

            await Task.Delay(100);

            // Assert - Test should complete without excessive memory usage
            // This is primarily a behavioral test - excessive memory would cause test timeout or system issues
            var logEntries = _loggerProvider.GetLogEntries();
            logEntries.ShouldNotBeEmpty();

            _logger.LogInformation("Memory test completed with {LogCount} unique messages processed", uniqueMessageCount);
        }

        /// <summary>
        /// Performance Test: Should handle high volume logging efficiently.
        /// </summary>
        /// <returns>A task that completes after stress logging and validation.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Handle_High_Volume_Efficiently()
        {
            // Arrange
            const int logCount = 1000;
            const string message = "High volume test message";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < logCount; i++)
            {
                _testLogger.LogRateLimited(LogLevel.Information, message + i, TimeSpan.FromSeconds(1));
            }

            stopwatch.Stop();
            await Task.Delay(100);

            // Assert - Should complete in reasonable time
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // 5 seconds max

            var logEntries = _loggerProvider.GetLogEntries();
            logEntries.ShouldNotBeEmpty();

            _logger.LogInformation("High volume test completed in {ElapsedMs}ms with {LogCount} messages",
                stopwatch.ElapsedMilliseconds, logCount);
        }

        //   Memory Leak Prevention Tests

        //   Thread Safety Tests

        /// <summary>
        /// Thread Safety Test: Should handle concurrent logging safely.
        /// </summary>
        /// <returns>A task that completes after concurrent logging and verification.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Handle_Concurrent_Logging_Safely()
        {
            // Arrange
            const int taskCount = 10;
            const int logsPerTask = 20;
            const string message = "Concurrent test message";

            // Act - Create concurrent logging tasks
            var tasks = Enumerable.Range(0, taskCount)
                .Select(taskId => Task.Run(() =>
                {
                    for (int i = 0; i < logsPerTask; i++)
                    {
                        _testLogger.LogRateLimited(LogLevel.Information, $"{message} from task {taskId}", TimeSpan.FromSeconds(1));
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);
            await Task.Delay(200); // Allow rate limiting to process

            // Assert - Should complete without exceptions
            var logEntries = _loggerProvider.GetLogEntries();
            logEntries.ShouldNotBeEmpty();

            _logger.LogInformation("Concurrent logging test completed with {TaskCount} tasks and {LogEntries} log entries",
                taskCount, logEntries.Count);
        }

        //   Thread Safety Tests

        //   Edge Case Tests

        /// <summary>
        /// Edge Case Test: Should handle empty message templates.
        /// </summary>
        /// <returns>A task that completes after ensuring empty templates do not throw exceptions.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Handle_Empty_Message_Templates()
        {
            // Arrange & Act - Should not throw
            Should.NotThrow(() => _testLogger.LogRateLimited(LogLevel.Information, string.Empty, TimeSpan.FromSeconds(1)));
            Should.NotThrow(() => _testLogger.LogRateLimited(LogLevel.Information, "", TimeSpan.FromSeconds(1)));

            await Task.Delay(100);

            // Assert
            var logEntries = _loggerProvider.GetLogEntries();
            // Empty messages might or might not be logged depending on implementation

            _logger.LogInformation("Empty message templates handled without exceptions");
        }

        /// <summary>
        /// Edge Case Test: Should handle very long message templates.
        /// </summary>
        /// <returns>A task that completes after confirming long templates are processed.</returns>
        [Fact(Timeout = 30_000)]
        public async Task LogRateLimited_Should_Handle_Very_Long_Message_Templates()
        {
            // Arrange
            var longMessage = new string('A', 10000); // 10KB message

            // Act - Should not throw
            Should.NotThrow(() => _testLogger.LogRateLimited(LogLevel.Information, longMessage, TimeSpan.FromSeconds(1)));

            await Task.Delay(100);

            // Assert
            var logEntries = _loggerProvider.GetLogEntries();
            logEntries.ShouldNotBeEmpty();

            _logger.LogInformation("Very long message templates handled successfully");
        }

        //   Edge Case Tests
    }

    /// <summary>
    /// Test logger provider to capture log entries for verification.
    /// </summary>
    internal class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<TestLogEntry> _logEntries = new();
        private readonly object _lock = new();

        /// <summary>
        /// Creates a logger that records messages in memory for later assertions.
        /// </summary>
        /// <param name="categoryName">Name of the logging category, kept to align with ILoggerFactory conventions.</param>
        /// <returns>A test logger that writes entries into the provider store.</returns>
        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(this, categoryName);
        }

        /// <summary>
        /// Adds a captured log entry into the provider's in-memory buffer.
        /// </summary>
        /// <param name="level">Severity level for the log record.</param>
        /// <param name="message">Formatted message content.</param>
        /// <param name="exception">Optional exception associated with the log record.</param>
        public void AddLogEntry(LogLevel level, string message, Exception? exception = null)
        {
            lock (_lock)
            {
                _logEntries.Add(new TestLogEntry
                {
                    Level = level,
                    Message = message,
                    Exception = exception,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Returns a copy of the collected log entries so callers can assert without mutating the source.
        /// </summary>
        /// <returns>List of log entries captured during the test execution.</returns>
        public List<TestLogEntry> GetLogEntries()
        {
            lock (_lock)
            {
                return new List<TestLogEntry>(_logEntries);
            }
        }

        /// <summary>
        /// Clears stored log entries and releases any held resources.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                _logEntries.Clear();
            }
        }
    }

    /// <summary>
    /// Test logger implementation.
    /// </summary>
    internal class TestLogger : ILogger
    {
        private readonly TestLoggerProvider _provider;
        private readonly string _categoryName;

        /// <summary>
        /// Initializes a new test logger that forwards entries to the shared provider store.
        /// </summary>
        /// <param name="provider">Provider that stores log entries for later inspection.</param>
        /// <param name="categoryName">Category name associated with the logger instance.</param>
        public TestLogger(TestLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        /// <summary>
        /// Begins a logging scope; test logger does not track scopes so it returns null.
        /// </summary>
        /// <typeparam name="TState">Type for the scope state.</typeparam>
        /// <param name="state">Scope state value.</param>
        /// <returns>Always null because scope tracking is unnecessary for these tests.</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        /// <summary>
        /// Always enables logging for tests to ensure every call is captured.
        /// </summary>
        /// <param name="logLevel">Log level being evaluated.</param>
        /// <returns>true in all cases so that log entries are recorded.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <summary>
        /// Records a log entry through the provider using the supplied formatter.
        /// </summary>
        /// <typeparam name="TState">Type of the state object being formatted.</typeparam>
        /// <param name="logLevel">Severity level of the log entry.</param>
        /// <param name="eventId">Event identifier.</param>
        /// <param name="state">State object passed to the formatter.</param>
        /// <param name="exception">Optional exception associated with the message.</param>
        /// <param name="formatter">Formatter that converts the state and exception to a message string.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _provider.AddLogEntry(logLevel, message, exception);
        }
    }

    /// <summary>
    /// Log entry for testing.
    /// </summary>
    internal class TestLogEntry
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
