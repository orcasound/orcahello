using OrcaHello.Web.Api.Models;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class ModeratorOrchestrationServiceTests
    {
        [TestMethod]
        public void Guard_AllGuardConditions_Expect_Exception()
        {
            var wrapper = new ModeratorOrchestrationServiceWrapper();

            DateTime? invalidDate = DateTime.MinValue;

            Assert.ThrowsException<InvalidModeratorOrchestrationException>(() =>
                wrapper.Validate(invalidDate, nameof(invalidDate)));

            DateTime? nullDate = null;

            Assert.ThrowsException<InvalidModeratorOrchestrationException>(() =>
                wrapper.Validate(nullDate, nameof(nullDate)));

            string invalidProperty = string.Empty;

            Assert.ThrowsException<InvalidModeratorOrchestrationException>(() =>
                wrapper.Validate(invalidProperty, nameof(invalidProperty)));

            int invalidPage = 0;

            Assert.ThrowsException<InvalidModeratorOrchestrationException>(() =>
                wrapper.ValidatePage(invalidPage));

            int invalidPageSize = 0;
            Assert.ThrowsException<InvalidModeratorOrchestrationException>(() =>
                wrapper.ValidatePageSize(invalidPageSize));
        }
    }
}
