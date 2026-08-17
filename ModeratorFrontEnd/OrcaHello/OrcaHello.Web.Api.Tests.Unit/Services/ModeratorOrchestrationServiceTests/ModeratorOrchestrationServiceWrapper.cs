using OrcaHello.Web.Api.Services;
using System.Diagnostics.CodeAnalysis;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    [ExcludeFromCodeCoverage]
    public class ModeratorOrchestrationServiceWrapper : ModeratorOrchestrationService
    {
        public new void Validate(DateTime? date, string propertyName) =>
            base.Validate(date, propertyName);

        public new void Validate(string propertyValue, string propertyName) =>
            base.Validate(propertyValue, propertyName);

        public new void ValidatePage(int page) =>
            base.ValidatePage(page);

        public new void ValidatePageSize(int pageSize) =>
            base.ValidatePageSize(pageSize);

        public new ValueTask<T> TryCatch<T>(ReturningGenericFunction<T> returningGenericFunction) =>
            base.TryCatch(returningGenericFunction);
    }
}