using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationSystem.Utilities;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace NotificationSystem.Tests.Unit
{
    public class SendFBPostTests
    {
        [Fact]
        public async Task ProcessDocumentsAsync_PostsForReviewedConfirmedRecord()
        {
            string json = """
                {
                  "reviewed": true,
                  "SRKWFound": "yes",
                  "timestamp": "2026-10-19T20:15:00Z",
                  "comments": "possible transient calls",
                  "location": { "name": "Bush Point" }
                }
                """;
            var documents = new List<JsonElement> { JsonDocument.Parse(json).RootElement };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["FB_POST_ENABLED"] = "true"
                })
                .Build();

            var logger = new Mock<ILogger<SendFBPost>>();
            var postService = new Mock<IFacebookPostService>();
            postService
                .Setup(service => service.PostToPageAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            var function = new SendFBPost(logger.Object, configuration, postService.Object);
            await function.ProcessDocumentsAsync(documents);

            postService.Verify(service => service.PostToPageAsync(It.Is<string>(message =>
                message.Contains("Transient Killer Whale") &&
                message.Contains("Bush Point"))), Times.Once);
        }

        [Fact]
        public async Task ProcessDocumentsAsync_DoesNotPostForUnreviewedRecord()
        {
            string json = """
                {
                  "reviewed": false,
                  "SRKWFound": "yes",
                  "timestamp": "2026-10-19T20:15:00Z",
                  "comments": "srkw call",
                  "location": { "name": "Bush Point" }
                }
                """;
            var documents = new List<JsonElement> { JsonDocument.Parse(json).RootElement };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["FB_POST_ENABLED"] = "true"
                })
                .Build();

            var logger = new Mock<ILogger<SendFBPost>>();
            var postService = new Mock<IFacebookPostService>();
            postService
                .Setup(service => service.PostToPageAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            var function = new SendFBPost(logger.Object, configuration, postService.Object);
            await function.ProcessDocumentsAsync(documents);

            postService.Verify(service => service.PostToPageAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessDocumentsAsync_DoesNotPostWhenSrkwNotConfirmed()
        {
            string json = """
                {
                  "reviewed": true,
                  "SRKWFound": "no",
                  "timestamp": "2026-10-19T20:15:00Z",
                  "comments": "srkw call",
                  "location": { "name": "Bush Point" }
                }
                """;
            var documents = new List<JsonElement> { JsonDocument.Parse(json).RootElement };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["FB_POST_ENABLED"] = "true"
                })
                .Build();

            var logger = new Mock<ILogger<SendFBPost>>();
            var postService = new Mock<IFacebookPostService>();
            postService
                .Setup(service => service.PostToPageAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            var function = new SendFBPost(logger.Object, configuration, postService.Object);
            await function.ProcessDocumentsAsync(documents);

            postService.Verify(service => service.PostToPageAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
