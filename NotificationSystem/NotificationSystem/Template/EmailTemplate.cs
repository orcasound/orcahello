using Newtonsoft.Json.Linq;
using NotificationSystem.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NotificationSystem.Template
{
    // TODO: we should move all html out of code and maybe use a preset email template for better design.
    public static class EmailTemplate
    {
        public static string GetModeratorEmailBody(DateTime? timestamp, string category, string location)
        {
            return $"<html><head><style>{GetCSS()}</style></head><body>{GetModeratorEmailHtml(timestamp, category, location)}</body></html>";
        }

        public static string GetSubscriberEmailBody(JObject message, string category, OrcasiteHelper orcasiteHelper = null)
        {
            return $"<html><head><style>{GetCSS()}</style></head><body>{GetSubscriberEmailHtml(message, category, orcasiteHelper)}</body></html>";
        }

        public static string GetCategory(string? comments)
        {
            if (comments?.Contains("transient") == true)
            {
                return "Transient Killer Whale";
            }
            if (comments?.Contains("humpback") == true)
            {
                return "Humpback";
            }
            return "Southern Resident Killer Whale";
        }

        public static string GetLocation(JObject message)
        {
            // Extract location from first message
            try
            {
                return message["location"]?["name"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static string GetModeratorEmailSubject(string category, string location)
        {
            return $"{category} Candidate at location {(string.IsNullOrEmpty(location) ? "Unknown" : location)}";
        }

        public static string GetSubscriberEmailSubject(string category, string location)
        {
            return $"Notification: {category} detected at location {(string.IsNullOrEmpty(location) ? "Unknown" : location)}";
        }

        private static string GetSubscriberEmailHtml(JObject message, string category, OrcasiteHelper orcasiteHelper)
        {
            string timeString = GetPacificTimestring((DateTime?) message["timestamp"]);

            return $@"
                <body>
                <div class='card'>
                <h1>
                {category} Detected
                </h1>
                <p>
                Dear subscriber, a {category} was most recently detected at around {timeString} Pacific.
                </p>
                <p>
                Please be mindful of their presence when travelling in the areas below.
                </p>
                <hr/>
                <h2>
                Detections
                </h2>
                <p>
                <center>
                  <table style='width:70%;'>
                  {GetDetectedSectionHtml(message, orcasiteHelper)}
                  </table>
                </center>
                </p>
                </div>
                <footer>
                  In partnership with Microsoft AI 4 Earth, Orcasound and Orca Conservancy.
                </footer>
                </body>
            ";
        }

        private static string GetDetectedSectionHtml(JObject message, OrcasiteHelper orcasiteHelper)
        {
            string timeString = GetPacificTimestring((DateTime?)message["timestamp"]);

            return $@"
                  <tr>
                    <td>
                      <img src='{GetMapUri((string)message["location"]["name"], orcasiteHelper)}'>
                    </td>
                    <td>
                      <ul>
                      <li><b>Time Detected:</b> {timeString} </li>
                      <li><b>Location:</b> {message["location"]["name"]} - {message["location"]["latitude"]}, {message["location"]["longitude"]} </li>
                      <li><b>Moderated By: </b> {message["moderator"]} </li>
                      </ul>
                      <p>
                        <center>
                          <b>Moderator Comments:</b><br>
                          {message["comments"]}
                        </center>
                      </p>
                    </td>
                  </tr>
                ";
        }

        private static string GetMapUri(string locationName, OrcasiteHelper orcasiteHelper)
        {
            // Try to get the slug from OrcasiteHelper first
            string slug = orcasiteHelper?.GetSlugByLocationName(locationName);

            if (string.IsNullOrEmpty(slug))
            {
                // Fall back to converting location name to lowercase and replacing spaces with hyphens
                slug = locationName.ToLower().Replace(" ", "-");
            }

            return $"https://orcanotificationstorage.blob.core.windows.net/images/{slug}.jpg";
        }

        private static string GetModeratorEmailHtml(DateTime? timestamp, string category, string location)
        {
            string timeString = GetPacificTimestring(timestamp);

            return $@"
                <body>
                <div class='card'>
                <h1>
                {category} Call Candidate
                </h1>
                <p>
                Dear moderator, a potential {category} call was detected on {timeString} Pacific at {location} location.
                </p>
                <p>
                This is a request for your moderation to confirm whether the sound was produced by a {category} on the portal below.
                </p>
                <hr/>
                <h2>
                Orca Moderation Portal
                </h2>
                <p>
                Please click the link below to move to the portal.
                </p>
                <a href='https://aifororcas.azurewebsites.net/' class='button-link'>
                Go to portal
                </a>
                </div>
                <footer>
                  <br>
                  <center>
                  In partnership with Microsoft AI 4 Earth, Orcasound and Orca Conservancy.
                  </center>
                </footer>
                </body>
            ";
        }

        private static string GetCSS()
        {
            return @"body {
                  font-family: 'Segoe UI', 'helvetica';
                  background-color: #F4F4F4;
                }

                .card {
                  background-color: white;
                  margin: 5%;
                  padding: 20px;
                }

                p {
                  color: dark-gray;
                }

                .button-link {
                  display: inline-block;
                  border: 0px;
                  background-color: #425AF4;
                  border-radius: 5px;
                  color: white;
                  font-size:20px;
                  padding:15px;
                  font-weight: bold;
                }

                footer {
                  text-align: center;
                  font-size:12px;
                }

                a {
                  text-decoration:none;
                  color: white;
                  font-weight: bold;
                }
            ";
        }

        private static string GetPacificTimestring(DateTime? timestamp)
        {
            var pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            return timestamp != null ? (TimeZoneInfo.ConvertTimeFromUtc(timestamp.Value, pacificTimeZone).ToShortDateString() + " " + TimeZoneInfo.ConvertTimeFromUtc(timestamp.Value, pacificTimeZone).ToLongTimeString()) : "unknown time";
        }
    }
}
