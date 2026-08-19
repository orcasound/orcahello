namespace OrcaHello.Web.Shared.Utilities
{
    [ExcludeFromCodeCoverage]
    public static class LoggingUtilities
    {
        public static void Log(ILogger logger, LogLevel level, Exception? exception, string message)
        {
            logger.Log(level, 1, exception, message);
        }

        public static void LogInfo(ILogger logger, string message, Exception? exception = null)
        {
            Log(logger, LogLevel.Information, exception, message);
        }

        public static void LogWarn(ILogger logger, string message, Exception? exception = null)
        {
            Log(logger, LogLevel.Warning, exception, message);
        }

        public static void LogWarn(ILogger logger, Exception exception)
        {
            if (exception is not null)
            {
                var innerException = exception.InnerException;

                if (innerException is not null)
                {
                    Log(logger, LogLevel.Warning, exception, innerException.Message);
                }
                else
                {
                    Log(logger, LogLevel.Warning, exception, exception.Message);
                }
            }
        }

        public static void LogError(ILogger logger, string message, Exception? exception = null)
        {
            Log(logger, LogLevel.Error, exception, message);
        }

        public static void LogError(ILogger logger, Exception exception)
        {
            if (exception is not null)
            {
                var innerException = exception.InnerException;

                if (innerException is not null)
                {
                    Log(logger, LogLevel.Error, exception, innerException.Message);
                }
                else
                {
                    Log(logger, LogLevel.Error, exception, exception.Message);
                }
            }
        }

        public static void LogTrace(ILogger logger, string message, Exception? exception = null)
        {
            Log(logger, LogLevel.Trace, exception, message);
        }

        public static void LogDebug(ILogger logger, string message, Exception? exception = null)
        {
            Log(logger, LogLevel.Debug, exception, message);
        }

        public static T CreateAndLogException<T>(ILogger logger, Exception innerException) where T : new()
        {
            T exception = (T)Activator.CreateInstance(typeof(T), innerException)!;

            if (logger is not null && exception is not null)
            {
                LogError(logger, exception as Exception);
            }

            return exception;
        }

        public static T CreateAndLogWarning<T>(ILogger logger, Exception innerException) where T : new()
        {
            T exception = (T)Activator.CreateInstance(typeof(T), innerException)!;

            if (logger is not null && exception is not null)
            {
                LogWarn(logger, exception as Exception);
            }

            return exception;
        }

        public static string MissingRequiredProperty(string propertyName) => $"Required property '{propertyName}' is invalid or missing from request.";

        public static string InvalidProperty(string propertyName) => $"Property '{propertyName}' in request is invalid.";

        public static string EmptyRequestBody() => "Request body is empty.";
    }
}
