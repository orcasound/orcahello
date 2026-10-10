namespace OrcaHello.Console.DataMigration.Tests.Unit
{
    [TestClass]
    public class DatabaseMigrationServiceTests
    {
        [TestMethod]
        public void NullLocation_ConvertToNewSchema_Expect_Null()
        {
            var item = new Metadata { id = "1", location = null };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void BlankLocationName_ConvertToNewSchema_Expect_Null()
        {
            var item = new Metadata { id = "1", location = new Location { name = "   " } };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ValidLocation_ConvertToNewSchema_Expect_MappedFields()
        {
            var item = new Metadata
            {
                id = "detection-1",
                audioUri = "audio.wav",
                imageUri = "image.png",
                timestamp = new DateTime(2024, 1, 1),
                location = new Location { name = "Lime Kiln" },
                whaleFoundConfidence = 90.1m,
                comments = "some comments",
                moderator = "mod@example.com",
                dateModerated = "2024-01-02",
                reviewed = true,
                SRKWFound = "yes"
            };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNotNull(result);
            Assert.AreEqual(item.id, result.id);
            Assert.AreEqual(item.audioUri, result.audioUri);
            Assert.AreEqual(item.imageUri, result.imageUri);
            Assert.AreEqual(item.timestamp, result.timestamp);
            Assert.AreEqual(item.whaleFoundConfidence, result.whaleFoundConfidence);
            Assert.AreEqual(item.comments, result.comments);
            Assert.AreEqual(item.moderator, result.moderator);
            Assert.AreEqual(item.dateModerated, result.dateModerated);
            Assert.AreEqual("Lime Kiln", result.locationName);
        }

        [TestMethod]
        public void HaroStraitLocation_ConvertToNewSchema_Expect_RenamedToOrcasoundLab()
        {
            var location = new Location { name = "Haro Strait" };
            var item = new Metadata { id = "1", location = location };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNotNull(result);
            Assert.AreEqual("Orcasound Lab", result.locationName);
            Assert.AreEqual("Orcasound Lab", location.name);
        }

        [TestMethod]
        public void SemicolonSeparatedTags_ConvertToNewSchema_Expect_SplitIntoList()
        {
            var item = new Metadata { id = "1", location = new Location { name = "Lime Kiln" }, tags = "S7;S10" };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new List<string> { "S7", "S10" }, result.tags);
        }

        [TestMethod]
        public void BlankTags_ConvertToNewSchema_Expect_DefaultEmptyList()
        {
            var item = new Metadata { id = "1", location = new Location { name = "Lime Kiln" }, tags = "" };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.tags.Count);
        }

        [TestMethod]
        public void NotReviewed_ConvertToNewSchema_Expect_UnreviewedState()
        {
            var item = new Metadata { id = "1", location = new Location { name = "Lime Kiln" }, reviewed = false };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNotNull(result);
            Assert.AreEqual("Unreviewed", result.state);
        }

        [TestMethod]
        public void ReviewedAndSRKWFoundYes_ConvertToNewSchema_Expect_PositiveState()
        {
            var item = new Metadata { id = "1", location = new Location { name = "Lime Kiln" }, reviewed = true, SRKWFound = "yes" };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNotNull(result);
            Assert.AreEqual("Positive", result.state);
        }

        [TestMethod]
        public void ReviewedAndSRKWFoundNo_ConvertToNewSchema_Expect_NegativeState()
        {
            var item = new Metadata { id = "1", location = new Location { name = "Lime Kiln" }, reviewed = true, SRKWFound = "no" };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNotNull(result);
            Assert.AreEqual("Negative", result.state);
        }

        [TestMethod]
        public void ReviewedAndSRKWFoundDontKnow_ConvertToNewSchema_Expect_UnknownState()
        {
            var item = new Metadata { id = "1", location = new Location { name = "Lime Kiln" }, reviewed = true, SRKWFound = "don't know" };

            var result = DatabaseMigrationService.ConvertToNewSchema(item);

            Assert.IsNotNull(result);
            Assert.AreEqual("Unknown", result.state);
        }
    }
}
