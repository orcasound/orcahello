using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationSystem.Utilities;
using RichardSzalay.MockHttp;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace NotificationSystem.Tests.Unit
{
    public class FacebookPostServiceTests
    {
        [Fact]
        public async Task PostToPageAsync_ReturnsTrue_WhenFacebookAcceptsPost()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["FB_PAGE_ID"] = "12345",
                    ["FB_PAGE_ACCESS_TOKEN"] = "test-token",
                    ["FB_GRAPH_API_VERSION"] = "v21.0"
                })
                .Build();

            var mockHttp = new MockHttpMessageHandler();
            var expectedRequest = mockHttp
                .When(HttpMethod.Post, "https://graph.facebook.com/v21.0/12345/feed")
                .Respond(HttpStatusCode.OK, "application/json", """{"id":"post-id"}""");

            var logger = new Mock<ILogger<FacebookPostService>>();
            var service = new FacebookPostService(mockHttp.ToHttpClient(), configuration, logger.Object);

            bool ok = await service.PostToPageAsync("test message");

            Assert.True(ok);
            Assert.Equal(1, mockHttp.GetMatchCount(expectedRequest));
        }

        [Fact]
        public async Task PostToPageAsync_ReturnsFalse_WhenMissingPageId()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["FB_PAGE_ACCESS_TOKEN"] = "test-token"
                })
                .Build();

            var mockHttp = new MockHttpMessageHandler();
            var logger = new Mock<ILogger<FacebookPostService>>();
            var service = new FacebookPostService(mockHttp.ToHttpClient(), configuration, logger.Object);

            bool ok = await service.PostToPageAsync("test message");

            Assert.False(ok);
        }
    }
}
