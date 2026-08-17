using System;

namespace AIForOrcas.Client.BL.Helpers
{
    public static class DateHelper
    {
        public static string UTCToPDT(DateTime datetime, bool timeOnly = false)
        {
            TimeZoneInfo pst = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            datetime = DateTime.SpecifyKind(datetime, DateTimeKind.Utc);
            DateTime pstTime = TimeZoneInfo.ConvertTime(datetime, TimeZoneInfo.Utc, pst);
            var zoneString = pst.IsDaylightSavingTime(pstTime) ? "PDT" : "PST";
            var format = timeOnly ? $"HH:mm:ss" : $"dd MMM HH:mm:ss '{zoneString}'";
            return $"{pstTime.ToString(format)}";
        }

        public static string UTCToPDTFull(DateTime datetime)
        {
            TimeZoneInfo pst = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            datetime = DateTime.SpecifyKind(datetime, DateTimeKind.Utc);
            DateTime pstTime = TimeZoneInfo.ConvertTime(datetime, TimeZoneInfo.Utc, pst);
            var zoneString = pst.IsDaylightSavingTime(pstTime) ? "PDT" : "PST";
            var format = $"dd MMM yyyy HH:mm:ss '{zoneString}'";
            return $"{pstTime.ToString(format)}";
        }
    }
}
