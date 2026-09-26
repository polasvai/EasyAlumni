using EasyAlumni.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyAlumni.Infrastructure.Logging
{
    public class LogsWebsiteLoggerProvider : ILoggerProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public LogsWebsiteLoggerProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new LogsWebsiteLogger(categoryName, _serviceProvider);
        }

        public void Dispose()
        {
        }
    }

    public class LogsWebsiteLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IServiceProvider _serviceProvider;

        public LogsWebsiteLogger(string categoryName, IServiceProvider serviceProvider)
        {
            _categoryName = categoryName;
            _serviceProvider = serviceProvider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            // Only capture Error and Critical logs
            return logLevel >= LogLevel.Error;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            // Avoid infinite recursion if the logging service or HTTP client itself logs an error
            if (_categoryName.Contains("LogsWebsite") || 
                _categoryName.Contains("System.Net.Http") || 
                _categoryName.Contains("Microsoft.EntityFrameworkCore"))
            {
                return;
            }

            var message = formatter(state, exception);
            var title = exception != null ? $"{exception.GetType().Name}: {exception.Message}" : message;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = $"Application {logLevel} occurred";
            }

            var severity = logLevel switch
            {
                LogLevel.Critical => "critical",
                _ => "error"
            };

            var dataSb = new System.Text.StringBuilder();
            dataSb.AppendLine($"Category: {_categoryName}");
            dataSb.AppendLine($"EventId: {eventId.Id} ({eventId.Name})");
            dataSb.AppendLine($"Message: {message}");

            if (exception != null)
            {
                dataSb.AppendLine();
                dataSb.AppendLine("Exception Details:");
                dataSb.AppendLine(exception.ToString());
            }

            var data = dataSb.ToString();

            // Run in background task to avoid blocking request thread
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var logsService = scope.ServiceProvider.GetService<ILogsWebsiteService>();
                    if (logsService != null)
                    {
                        await logsService.LogAsync(title, severity, _categoryName, data);
                    }
                }
                catch
                {
                    // Fail silently to never crash the main application
                }
            });
        }
    }
}
