using System;

namespace SPL_API
{
    internal static class AppUtilities
    {
        public static string Base64Encode(string plainString)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(plainString);
            return System.Convert.ToBase64String(data);
        }

        public static string Base64Decode(string encodedString)
        {
            byte[] data = Convert.FromBase64String(encodedString);
            return System.Text.Encoding.UTF8.GetString(data);
        }

        public static DateTime GenerateDateTimeMondayMidnight()
        {
            var date = DateTime.UtcNow;
            return date.AddDays(-((7 + (int)date.DayOfWeek) - (int)DayOfWeek.Monday) % 7).AddHours(-date.Hour).AddMinutes(-date.Minute).AddSeconds(-date.Second).AddMilliseconds(-date.Millisecond);
        }
    }
}
