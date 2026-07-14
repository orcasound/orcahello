using AIForOrcas.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NotificationSystem.Models;
using NotificationSystem.Template;
using NotificationSystem.Tests.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NotificationSystem.Tests.Unit
{
    public class EmailTemplateTests
    {
        #region Subscriber Tests

        /// <summary>
        /// Tests that GetSubscriberEmailBody generates correct map URIs for various locations
        /// by verifying the generated HTML contains the expected image URLs.
        /// Uses OrcasiteHelper initialization to simulate production behavior.
        /// </summary>
        [Theory]
        [MemberData(nameof(HydrophoneLocationMapUriData))]
        public void GetSubscriberEmailBody_GeneratesCorrectMapUri_ForLocationName(string locationName, string expectedFileName)
        {
            // Arrange
            var message = JObject.FromObject(new
            {
                timestamp = DateTime.UtcNow,
                location = new
                {
                    name = locationName,
                    latitude = 48.123,
                    longitude = -122.456,
                    id = "test_location"
                },
                moderator = "Test Moderator",
                comments = "Test comments"
            });

            var orcasiteHelper = CreateInitializedOrcasiteHelper();

            string expectedMapUrl = $"https://orcanotificationstorage.blob.core.windows.net/images/{expectedFileName}";

            // Act - with OrcasiteHelper as in production
            string emailBody = EmailTemplate.GetSubscriberEmailBody(message, "Southern Resident Killer Whale", orcasiteHelper);

            // Assert
            Assert.Contains(expectedMapUrl, emailBody);
        }

        public static IEnumerable<object[]> HydrophoneLocationMapUriData =>
            GetHydrophoneLocationMapUriData();

        /// <summary>
        /// Tests that location names with multiple words are correctly converted with hyphens in URIs.
        /// </summary>
        [Fact]
        public void GetSubscriberEmailBody_HandlesMultipleSpacesCorrectly()
        {
            // Arrange
            var message = JObject.FromObject(new
            {
                timestamp = DateTime.UtcNow,
                location = new
                {
                    name = "North San Juan Channel",
                    latitude = 48.591294,
                    longitude = -123.058779,
                    id = "rpi_north_sjc"
                },
                moderator = "Test Moderator",
                comments = "Test comments"
            });

            var orcasiteHelper = CreateInitializedOrcasiteHelper();

            // Act - with OrcasiteHelper as in production
            string emailBody = EmailTemplate.GetSubscriberEmailBody(message, "Southern Resident Killer Whale", orcasiteHelper);

            // Assert - the URI should use "north-sjc" from OrcasiteHelper
            Assert.Contains("north-sjc.jpg", emailBody);
            Assert.DoesNotContain("north-san-juan-channel.jpg", emailBody);
            // The location name should still display with spaces
            Assert.Contains("North San Juan Channel", emailBody);
        }

        /// <summary>
        /// Tests that the fallback behavior works when OrcasiteHelper is not available.
        /// </summary>
        [Fact]
        public void GetSubscriberEmailBody_FallsBackToSimpleTransformation_WhenOrcasiteHelperNotProvided()
        {
            // Arrange
            var message = JObject.FromObject(new
            {
                timestamp = DateTime.UtcNow,
                location = new
                {
                    name = "Sunset Bay",
                    latitude = 47.86497296593844,
                    longitude = -122.33393605795372,
                    id = "rpi_sunset_bay"
                },
                moderator = "Test Moderator",
                comments = "Test comments"
            });

            // Act - without OrcasiteHelper, it falls back to simple transformation
            string emailBody = EmailTemplate.GetSubscriberEmailBody(message, "Southern Resident Killer Whale", null);

            // Assert - should use simple transformation
            Assert.Contains("sunset-bay.jpg", emailBody);
            Assert.Contains("Sunset Bay", emailBody);
        }

        /// <summary>
        /// Tests that the email body contains all required sections and location information.
        /// </summary>
        [Fact]
        public void GetSubscriberEmailBody_ContainsAllRequiredSections()
        {
            // Arrange
            var testTimestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            var message = JObject.FromObject(new
            {
                timestamp = testTimestamp,
                location = new
                {
                    name = "Sunset Bay",
                    latitude = 47.86497296593844,
                    longitude = -122.33393605795372,
                    id = "rpi_sunset_bay"
                },
                moderator = "Jane Doe",
                comments = "Clear SRKW calls detected"
            });

            // Act - without OrcasiteHelper, it falls back to simple transformation
            string emailBody = EmailTemplate.GetSubscriberEmailBody(message, "Southern Resident Killer Whale", null);

            // Assert
            Assert.Contains("Southern Resident Killer Whale Detected", emailBody);
            Assert.Contains("Sunset Bay", emailBody);
            Assert.Contains("47.86497296593844", emailBody);
            Assert.Contains("-122.33393605795372", emailBody);
            Assert.Contains("Jane Doe", emailBody);
            Assert.Contains("Clear SRKW calls detected", emailBody);
            Assert.Contains("https://orcanotificationstorage.blob.core.windows.net/images/sunset-bay.jpg", emailBody);
        }

        /// <summary>
        /// Tests that GetSubscriberEmailBody uses OrcasiteHelper to lookup the correct slug when provided.
        /// </summary>
        [Fact]
        public void GetSubscriberEmailBody_UsesOrcasiteHelperSlug_WhenProvided()
        {
            // Arrange
            var message = JObject.FromObject(new
            {
                timestamp = DateTime.UtcNow,
                location = new
                {
                    name = "North San Juan Channel",
                    latitude = 48.591294,
                    longitude = -123.058779,
                    id = "rpi_north_sjc"
                },
                moderator = "Test Moderator",
                comments = "Test comments"
            });

            var orcasiteHelper = CreateInitializedOrcasiteHelper();
            
            // Act
            string emailBody = EmailTemplate.GetSubscriberEmailBody(message, "Southern Resident Killer Whale", orcasiteHelper);

            // Assert - should use "north-sjc" from OrcasiteHelper, not "north-san-juan-channel"
            Assert.Contains("north-sjc.jpg", emailBody);
            Assert.DoesNotContain("north-san-juan-channel.jpg", emailBody);
            Assert.Contains("North San Juan Channel", emailBody);
        }

        /// <summary>
        /// Tests that GetSubscriberEmailSubject generates correct subject line with location.
        /// </summary>
        [Fact]
        public void GetSubscriberEmailSubject_IncludesLocationInSubject()
        {
            // Arrange
            var message = JObject.FromObject(new
            {
                timestamp = DateTime.UtcNow,
                location = new
                {
                    name = "Sunset Bay",
                    latitude = 47.86497296593844,
                    longitude = -122.33393605795372,
                    id = "rpi_sunset_bay"
                },
                moderator = "Test Moderator",
                comments = "Test comments"
            });

            // Act
            string location = EmailTemplate.GetLocation(message);
            string subject = EmailTemplate.GetSubscriberEmailSubject("Southern Resident Killer Whale", location);

            // Assert
            Assert.Equal("Notification: Southern Resident Killer Whale detected at Sunset Bay", subject);
        }

        /// <summary>
        /// Tests that GetSubscriberEmailSubject handles empty location with "Unknown".
        /// </summary>
        [Fact]
        public void GetSubscriberEmailSubject_HandlesEmptyLocation()
        {
            // Arrange
            var message = JObject.FromObject(new
            {
                timestamp = DateTime.UtcNow,
                location = new
                {
                    name = "",
                    latitude = 47.86497296593844,
                    longitude = -122.33393605795372,
                    id = "rpi_sunset_bay"
                },
                moderator = "Test Moderator",
                comments = "Test comments"
            });

            // Act
            string location = EmailTemplate.GetLocation(message);
            string subject = EmailTemplate.GetSubscriberEmailSubject("Southern Resident Killer Whale", location);

            // Assert
            Assert.Equal("Notification: Southern Resident Killer Whale detected at Unknown", subject);
        }

        /// <summary>
        /// Tests that GetSubscriberEmailSubject handles null location with "Unknown".
        /// </summary>
        [Fact]
        public void GetSubscriberEmailSubject_HandlesNullLocation()
        {
            // Arrange
            string category = "Southern Resident Killer Whale";
            string location = null;

            // Act
            string subject = EmailTemplate.GetSubscriberEmailSubject(category, location);

            // Assert
            Assert.Equal("Notification: Southern Resident Killer Whale detected at Unknown", subject);
        }
        
        #endregion

        #region Moderator Tests

        /// <summary>
        /// Tests that GetModeratorEmailBody contains all required sections.
        /// </summary>
        [Fact]
        public void GetModeratorEmailBody_ContainsAllRequiredSections()
        {
            // Arrange
            var testTimestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            string category = "Southern Resident Killer Whale";
            string location = "Sunset Bay";

            // Act
            string emailBody = EmailTemplate.GetModeratorEmailBody(testTimestamp, category, location);

            // Assert
            Assert.Contains("Southern Resident Killer Whale Call Candidate", emailBody);
            Assert.Contains(category, emailBody);
            Assert.Contains(location, emailBody);
            Assert.Contains("Orca Moderation Portal", emailBody);
            Assert.Contains("https://aifororcas.azurewebsites.net/", emailBody);
        }

        /// <summary>
        /// Tests that GetModeratorEmailBody handles null timestamp gracefully.
        /// </summary>
        [Fact]
        public void GetModeratorEmailBody_HandlesNullTimestamp()
        {
            // Arrange
            DateTime? testTimestamp = null;
            string category = "Southern Resident Killer Whale";
            string location = "Sunset Bay";

            // Act
            string emailBody = EmailTemplate.GetModeratorEmailBody(testTimestamp, category, location);

            // Assert
            Assert.Contains("unknown time", emailBody);
            Assert.Contains("Southern Resident Killer Whale Call Candidate", emailBody);
        }

        /// <summary>
        /// Tests that GetModeratorEmailBody formats timestamp to Pacific correctly.
        /// </summary>
        [Fact]
        public void GetModeratorEmailBody_FormatsTimestampToPacific()
        {
            // Arrange
            var testTimestamp = new DateTime(2025, 1, 15, 18, 30, 0, DateTimeKind.Utc); // 6:30 PM UTC
            string category = "Southern Resident Killer Whale";
            string location = "Sunset Bay";

            // Act
            string emailBody = EmailTemplate.GetModeratorEmailBody(testTimestamp, category, location);

            // Assert
            // UTC 18:30 converts to PST 10:30 (UTC-8 during standard time)
            Assert.Contains("Pacific", emailBody);
            // Verify the date is present
            Assert.Contains("1/15/2025", emailBody);
        }

        /// <summary>
        /// Tests that GetModeratorEmailBody includes proper HTML structure.
        /// </summary>
        [Fact]
        public void GetModeratorEmailBody_IncludesValidHtmlStructure()
        {
            // Arrange
            var testTimestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            string category = "Southern Resident Killer Whale";
            string location = "Sunset Bay";

            // Act
            string emailBody = EmailTemplate.GetModeratorEmailBody(testTimestamp, category, location);

            // Assert
            Assert.StartsWith("<html>", emailBody);
            Assert.Contains("<style>", emailBody);
            Assert.Contains("</style>", emailBody);
            Assert.Contains("<body>", emailBody);
            Assert.Contains("</body>", emailBody);
            Assert.EndsWith("</html>", emailBody);
        }

        /// <summary>
        /// Tests that GetModeratorEmailSubject generates correct subject line with category and location.
        /// </summary>
        [Fact]
        public void GetModeratorEmailSubject_IncludesCategoryAndLocation()
        {
            // Arrange
            string category = "Southern Resident Killer Whale";
            string location = "Sunset Bay";

            // Act
            string subject = EmailTemplate.GetModeratorEmailSubject(category, location);

            // Assert
            Assert.Equal("Southern Resident Killer Whale Candidate at Sunset Bay", subject);
        }

        /// <summary>
        /// Tests that GetModeratorEmailSubject handles empty location with "Unknown".
        /// </summary>
        [Fact]
        public void GetModeratorEmailSubject_HandlesEmptyLocation()
        {
            // Arrange
            string category = "Southern Resident Killer Whale";
            string location = "";

            // Act
            string subject = EmailTemplate.GetModeratorEmailSubject(category, location);

            // Assert
            Assert.Equal("Southern Resident Killer Whale Candidate at Unknown", subject);
        }

        /// <summary>
        /// Tests that GetModeratorEmailSubject handles null location with "Unknown".
        /// </summary>
        [Fact]
        public void GetModeratorEmailSubject_HandlesNullLocation()
        {
            // Arrange
            string category = "Southern Resident Killer Whale";
            string location = null;

            // Act
            string subject = EmailTemplate.GetModeratorEmailSubject(category, location);

            // Assert
            Assert.Equal("Southern Resident Killer Whale Candidate at Unknown", subject);
        }

        /// <summary>
        /// Tests that GetModeratorEmailBody works with different category types.
        /// </summary>
        [Theory]
        [InlineData("Southern Resident Killer Whale")]
        [InlineData("Transient Killer Whale")]
        [InlineData("Humpback")]
        [InlineData("Other")]
        public void GetModeratorEmailBody_WorksWithDifferentCategories(string category)
        {
            // Arrange
            var testTimestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            string location = "Sunset Bay";

            // Act
            string emailBody = EmailTemplate.GetModeratorEmailBody(testTimestamp, category, location);

            // Assert
            Assert.Contains(category, emailBody);
            Assert.Contains($"{category} Call Candidate", emailBody);
            Assert.Contains(location, emailBody);
        }

        /// <summary>
        /// Tests that GetModeratorEmailBody includes the portal link button.
        /// </summary>
        [Fact]
        public void GetModeratorEmailBody_IncludesPortalLinkButton()
        {
            // Arrange
            var testTimestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            string category = "Southern Resident Killer Whale";
            string location = "Sunset Bay";

            // Act
            string emailBody = EmailTemplate.GetModeratorEmailBody(testTimestamp, category, location);

            // Assert
            Assert.Contains("Go to portal", emailBody);
            Assert.Contains("button-link", emailBody);
            Assert.Contains("https://aifororcas.azurewebsites.net/", emailBody);
        }

        #endregion

        /// <summary>
        /// Tests that GetLocation handles null location.
        /// </summary>
        [Fact]
        public void GetLocation_HandlesNullLocation()
        {
            // Arrange
            var message = JObject.FromObject(new
            {
                timestamp = DateTime.UtcNow,
                moderator = "Test Moderator",
                comments = "Test comments"
            });

            // Act
            string location = EmailTemplate.GetLocation(message);

            // Assert
            Assert.Null(location);
        }

        private static string GetExpectedFileName(string locationName)
        {
            string hydrophoneId = HydrophoneLocations.GetIdByLocation(locationName)
                ?? throw new InvalidOperationException($"No hydrophone ID found for location '{locationName}'.");

            return hydrophoneId.Replace("rpi_", string.Empty).Replace('_', '-');
        }

        private static IEnumerable<object[]> GetHydrophoneLocationMapUriData()
        {
            _ = CreateInitializedOrcasiteHelper();

            return HydrophoneLocations.Locations
                .Select(locationName => new object[]
                {
                    locationName,
                    $"{GetExpectedFileName(locationName)}.jpg"
                });
        }

        private static OrcasiteHelper CreateInitializedOrcasiteHelper()
        {
            var container = OrcasiteTestHelper.GetMockOrcasiteHelperWithRequestVerification(
                new Mock<ILogger<OrcasiteHelper>>().Object);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ORCASITE_HOSTNAME"] = "live.orcasound.net"
                })
                .Build();

            container.Helper.InitializeAsync(configuration).GetAwaiter().GetResult();
            return container.Helper;
        }

        /// <summary>
        /// Tests that GetCategory correctly identifies different whale categories from comments.
        /// </summary>
        [Theory]
        [InlineData("AI: resident", "Southern Resident Killer Whale")]
        [InlineData("AI: resident and vessel", "Southern Resident Killer Whale")]
        [InlineData("AI: transient", "Transient Killer Whale")]
        [InlineData("AI: transient and vessel", "Transient Killer Whale")]
        [InlineData("AI: humpback", "Humpback")]
        [InlineData("AI: humpback and vessel", "Humpback")]
        [InlineData("Other", "Southern Resident Killer Whale")] // Default case
        [InlineData("", "Southern Resident Killer Whale")] // Empty string default
        [InlineData(null, "Southern Resident Killer Whale")] // Null default
        public void GetCategory_IdentifiesCorrectCategory(string? comments, string expectedCategory)
        {
            // Act
            string category = EmailTemplate.GetCategory(comments);

            // Assert
            Assert.Equal(expectedCategory, category);
        }
    }
}
