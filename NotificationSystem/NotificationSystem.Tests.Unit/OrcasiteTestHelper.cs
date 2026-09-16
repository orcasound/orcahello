using Microsoft.Azure.Documents;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NotificationSystem.Models;
using RichardSzalay.MockHttp;
using System.Net;
using System.Text.Json;

namespace NotificationSystem.Tests.Common
{
    public class OrcasiteTestHelper
    {
        private static string _solutionDirectory;

        /// <summary>
        /// Find the solution directory.
        /// </summary>
        /// <returns>Directory, or null if not found</returns>
        public static string? FindSolutionDirectory()
        {
            if (_solutionDirectory != null)
            {
                return _solutionDirectory;
            }
            string? currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            while (currentDirectory != null)
            {
                string path = Path.Combine(currentDirectory, "NotificationSystem.slnx");
                if (File.Exists(path))
                {
                    _solutionDirectory = currentDirectory;
                    return currentDirectory;
                }

                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }
            return null;
        }

        /// <summary>
        /// Get the contents of a TestData file as a string.
        /// </summary>
        /// <param name="filename">Name of file to load</param>
        /// <returns>String contents</returns>
        private static string GetStringFromFile(string filename)
        {
            string solutionDirectory = FindSolutionDirectory() ?? throw new Exception("Could not find solution directory");
            return File.ReadAllText(Path.Combine(solutionDirectory, "TestData", filename));
        }

        /// <summary>
        /// Get a mock OrcasiteHelper along with the MockHttpMessageHandler for verification.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <returns>MockOrcasiteHelperContainer containing all mock components for verification</returns>
        public static MockOrcasiteHelperContainer GetMockOrcasiteHelperWithRequestVerification(ILogger<OrcasiteHelper> logger)
        {
            var mockHttp = new MockHttpMessageHandler();
            string sampleOrcasiteFeeds = GetStringFromFile("OrcasiteFeeds.json");
            string sampleOrcasitePostDetectionResponse = GetStringFromFile("OrcasitePostDetectionResponse.json");

            // Mock the GET request to fetch feeds.
            var getFeedsRequest = mockHttp.When(HttpMethod.Get, "https://*.orcasound.net/api/json/feeds?fields%5Bfeed%5D=id%2Cname%2Cnode_name%2Cslug%2Clocation_point%2Cintro_html%2Cimage_url%2Cvisible%2Cbucket%2Cbucket_region%2Ccloudfront_url%2Cdataplicity_id%2Corcahello_id")
                    .Respond("application/json", sampleOrcasiteFeeds);

            // Mock the POST request to create a detection.
            var postDetectionRequest = mockHttp.When(HttpMethod.Post, "https://*.orcasound.net/api/json/detections?fields%5Bdetection%5D=id%2Csource_ip%2Cplaylist_timestamp%2Cplayer_offset%2Clistener_count%2Ctimestamp%2Cdescription%2Cvisible%2Csource%2Ccategory%2Ccandidate_id%2Cfeed_id")
                    //.WithContent("{\"key\":\"value\"}") // Optional: match request body
                    .Respond(HttpStatusCode.Created, "application/json", sampleOrcasitePostDetectionResponse);

            var httpClient = mockHttp.ToHttpClient();
            var orcasiteHelper = new OrcasiteHelper(logger, httpClient);
            return new MockOrcasiteHelperContainer(orcasiteHelper, mockHttp, getFeedsRequest, postDetectionRequest);
        }

        /// <summary>
        /// Wrapper class to hold both OrcasiteHelper and MockHttpMessageHandler together
        /// for dependency injection scenarios.
        /// </summary>
        public class MockOrcasiteHelperContainer
        {
            public OrcasiteHelper Helper { get; }
            public MockHttpMessageHandler MockHttp { get; }
            public MockedRequest GetFeedsRequest { get; }
            public MockedRequest PostDetectionRequest { get; }

            public MockOrcasiteHelperContainer(OrcasiteHelper helper, MockHttpMessageHandler mockHttp, MockedRequest getFeedsRequest, MockedRequest postDetectionRequest)
            {
                Helper = helper;
                MockHttp = mockHttp;
                GetFeedsRequest = getFeedsRequest;
                PostDetectionRequest = postDetectionRequest;
            }

            public MockOrcasiteHelperContainer(ILogger<OrcasiteHelper> logger)
            {
                var container = GetMockOrcasiteHelperWithRequestVerification(logger);
                Helper = container.Helper;
                MockHttp = container.MockHttp;
                GetFeedsRequest = container.GetFeedsRequest;
                PostDetectionRequest = container.PostDetectionRequest;
            }
        }

        public static List<JsonElement> GetSampleOrcaHelloDetections()
        {
            string sampleOrcaHelloDetection = GetStringFromFile("OrcaHelloDetection.json");
            JsonElement testDocument = JsonDocument.Parse(sampleOrcaHelloDetection).RootElement;
            var documents = new List<JsonElement> { testDocument };
            return documents;
        }
    }
}
