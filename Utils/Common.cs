using System.Collections.Specialized;

namespace Devil7Softwares.TeraboxDownloader.Utils;

internal static class Common
{
    public static string ToSizeString(this double byteCount)
    {
        return ((long)byteCount).ToSizeString();
    }

    public static string ToSizeString(this long byteCount)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
        if (byteCount == 0)
            return "0" + suf[0];
        long bytes = Math.Abs(byteCount);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return (Math.Sign(byteCount) * num).ToString() + suf[place];
    }

    public static string ToTimeString(this TimeSpan timeSpan)
    {
        string result = "";

        double flooredHours = Math.Floor(timeSpan.TotalHours);

        if (flooredHours > 0)
        {
            result = $"{Math.Floor(timeSpan.TotalHours)} hour{(flooredHours > 1 ? "s" : "")} ";
        }

        if (timeSpan.Minutes > 0)
        {
            result += $"{timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : "")} ";
        }

        if (timeSpan.Seconds > 0)
        {
            result += $"{timeSpan.Seconds} second{(timeSpan.Seconds > 1 ? "s" : "")}";
        }

        result = result.Trim();

        return string.IsNullOrEmpty(result) ? "0 seconds" : result;
    }

    public static string? GetQueryValue(this Uri uri, string key)
    {
        string queryString = uri.Query;

        if (!string.IsNullOrEmpty(queryString))
        {
            NameValueCollection query = System.Web.HttpUtility.ParseQueryString(queryString);

            if (query.HasKeys())
            {
                return query[key];
            }
        }

        return null;
    }
}