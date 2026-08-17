namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class TagViewServiceTests
    {
        [TestMethod]
        public void Guard_AllGuardConditions_Expect_Exception()
        {
            var wrapper = new TagViewServiceWrapper();

            List<string> badIds = null!;

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidateAtLeastOneTagSelected(badIds));

            badIds = new();

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidateAtLeastOneTagSelected(badIds));

            DateTime? invalidDate = DateTime.UtcNow.AddDays(1);
            DateTime? validDate = DateTime.UtcNow;

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidateDateRange(invalidDate, validDate));

            invalidDate = DateTime.UtcNow.AddDays(-1);

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidateDateRange(validDate, invalidDate));

            invalidDate = null;

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidateDateRange(invalidDate, validDate));

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidateDateRange(validDate, invalidDate));

            int invalidPage = -1;
            int validPageSize = 10;

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidatePagination(invalidPage, validPageSize));

            int validPage = 1;
            int invalidPageSize = -1;

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidatePagination(validPage, invalidPageSize));

            string invalidTagString = null!;

            Assert.ThrowsException<InvalidTagViewException>(() =>
                wrapper.ValidateTagString(invalidTagString, "invalidTagString"));

            ReplaceTagRequest? invalidRequest = null;

            Assert.ThrowsException<NullTagViewRequestException>(() =>
                wrapper.ValidateRequest(invalidRequest));

            TagReplaceResponse? invalidResponse = null;

            Assert.ThrowsException<NullTagViewResponseException>(() =>
                wrapper.ValidateResponse(invalidResponse));
        }
    }
}
