using System.Globalization;

namespace Parking.Api;

internal static class BusinessTime
{
    private static readonly string[] TimeZoneIds =
    [
        "America/Sao_Paulo",
        "E. South America Standard Time",
    ];

    private static readonly Lazy<TimeZoneInfo> SaoPauloZone = new(ResolveSaoPauloZone);

    internal static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) CurrentSaoPauloDayUtcRange(DateTimeOffset nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, SaoPauloZone.Value);
        var day = DateOnly.FromDateTime(localNow.DateTime);
        var startLocal = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, SaoPauloZone.Value), TimeSpan.Zero);
        var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, SaoPauloZone.Value), TimeSpan.Zero);
        return (startUtc, endUtc);
    }

    private static TimeZoneInfo ResolveSaoPauloZone()
    {
        foreach (var id in TimeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            id: "UTC-03",
            baseUtcOffset: TimeSpan.FromHours(-3),
            displayName: "UTC-03",
            standardDisplayName: "UTC-03");
    }
}
