namespace RentMaster.Infrastructure.Services;

/// <summary>
/// Supplies the current calendar date for India. Rental start/end dates are
/// local calendar dates, so deriving them directly from UTC can be one day
/// behind shortly after midnight in India.
/// </summary>
public sealed class IndiaDateProvider
{
    private static readonly TimeZoneInfo IndiaTimeZone = ResolveIndiaTimeZone();

    public DateOnly Today
    {
        get
        {
            var indiaNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, IndiaTimeZone);
            return DateOnly.FromDateTime(indiaNow.DateTime);
        }
    }

    private static TimeZoneInfo ResolveIndiaTimeZone()
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next cross-platform identifier.
            }
            catch (InvalidTimeZoneException)
            {
                // Try the next cross-platform identifier.
            }
        }

        // India does not observe daylight saving time. This fallback keeps
        // local/dev/UAT date rules correct even on a minimal host image.
        return TimeZoneInfo.CreateCustomTimeZone(
            "RentMaster-India",
            TimeSpan.FromHours(5.5),
            "India Standard Time",
            "India Standard Time");
    }
}
