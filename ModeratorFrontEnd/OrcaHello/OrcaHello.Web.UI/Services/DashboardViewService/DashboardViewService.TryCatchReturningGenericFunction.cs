namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="DashboardViewService"/> orchestration service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class DashboardViewService
    {
        public delegate ValueTask<T> ReturningGenericFunction<T>();

        protected async ValueTask<T> TryCatch<T>(ReturningGenericFunction<T> returningGenericFunction)
        {
            try
            {
                return await returningGenericFunction();
            }
            catch (Exception exception)
            {
                // If the exception is one of the following types, rethrow it as a DashboardViewValidationException and log it.
                // These exceptions indicate that there is something wrong with the view service itself or the request or response objects.
                if (exception is InvalidDashboardViewException ||
                    exception is NullDashboardViewRequestException ||
                    exception is NullDashboardViewResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<DashboardViewValidationException>(_logger, exception);
                }

                // If the exception is one of the following types, rethrow it as a DashboardViewDependencyValidationException and log it.
                // These exceptions indicate that there is something wrong with the validation of the entities or their dependencies that are shown in the dashboard.
                if (exception is DetectionValidationException ||
                    exception is DetectionDependencyValidationException ||
                    exception is TagValidationException ||
                    exception is TagDependencyValidationException ||
                    exception is MetricsValidationException ||
                    exception is MetricsDependencyValidationException ||
                    exception is CommentValidationException ||
                    exception is CommentDependencyValidationException ||
                    exception is ModeratorValidationException ||
                    exception is ModeratorDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<DashboardViewDependencyValidationException>(_logger, exception);
                }

                // If the exception is one of the following types, rethrow it as a DashboardViewDependencyException and log it.
                // These exceptions indicate that there is something wrong with the dependency services or the communication with them.
                if (exception is DetectionDependencyException ||
                    exception is DetectionServiceException ||
                    exception is TagDependencyException ||
                    exception is TagServiceException ||
                    exception is MetricsDependencyException ||
                    exception is MetricsServiceException ||
                    exception is CommentDependencyException ||
                    exception is CommentServiceException ||
                    exception is ModeratorDependencyException ||
                    exception is ModeratorServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<DashboardViewDependencyException>(_logger, exception);
                }

                // If the exception is any other type, rethrow it as a DashboardViewServiceException and log it.
                // This is a generic exception that indicates that something unexpected happened in the view service.
                throw LoggingUtilities.CreateAndLogException<DashboardViewServiceException>(_logger, exception);
            }
        }
    }
}