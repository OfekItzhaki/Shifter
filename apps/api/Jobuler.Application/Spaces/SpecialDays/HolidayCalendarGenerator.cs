using System.Globalization;
using Jobuler.Domain.Spaces;

namespace Jobuler.Application.Spaces.SpecialDays;

public static class HolidayCalendarGenerator
{
    private static readonly HebrewCalendar HebrewCalendar = new();

    public static IReadOnlyList<HolidayCalendarDayDto> Generate(
        string countryCode,
        int gregorianYear,
        IReadOnlySet<(DateOnly Date, string Name)> existing)
    {
        if (gregorianYear < 2000 || gregorianYear > 2100)
            throw new ArgumentException("Calendar year must be between 2000 and 2100.");

        var normalizedCountry = countryCode.Trim().ToUpperInvariant();
        if (normalizedCountry is not ("IL" or "ISR" or "ISRAEL"))
            throw new ArgumentException("Only the Israel holiday calendar is currently supported.");

        var start = new DateOnly(gregorianYear, 1, 1);
        var end = new DateOnly(gregorianYear, 12, 31);
        var hebrewYears = Enumerable.Range(
                HebrewCalendar.GetYear(start.ToDateTime(new TimeOnly(12, 0))) - 1,
                4)
            .Distinct();

        return hebrewYears
            .SelectMany(CreateIsraelHolidaysForHebrewYear)
            .Where(day => day.Date >= start && day.Date <= end)
            .OrderBy(day => day.Date)
            .ThenBy(day => day.Name)
            .Select(day => day with { AlreadyExists = existing.Contains((day.Date, day.Name)) })
            .ToList();
    }

    private static IEnumerable<HolidayCalendarDayDto> CreateIsraelHolidaysForHebrewYear(int hebrewYear)
    {
        yield return Create(hebrewYear, month: 1, day: 1, "Rosh Hashanah", 2.5m);
        yield return Create(hebrewYear, month: 1, day: 2, "Rosh Hashanah II", 2.5m);
        yield return Create(hebrewYear, month: 1, day: 10, "Yom Kippur", 3m);
        yield return Create(hebrewYear, month: 1, day: 15, "Sukkot", 2m);
        yield return Create(hebrewYear, month: 1, day: 22, "Shemini Atzeret / Simchat Torah", 2m);

        var nisan = HebrewCalendar.IsLeapYear(hebrewYear) ? 8 : 7;
        var sivan = HebrewCalendar.IsLeapYear(hebrewYear) ? 10 : 9;

        yield return Create(hebrewYear, nisan, day: 15, "Passover", 2m);
        yield return Create(hebrewYear, nisan, day: 21, "Passover VII", 2m);
        yield return Create(hebrewYear, sivan, day: 6, "Shavuot", 2m);
    }

    private static HolidayCalendarDayDto Create(
        int hebrewYear,
        int month,
        int day,
        string name,
        decimal homeLeaveWeightMultiplier) =>
        new(
            DateOnly.FromDateTime(HebrewCalendar.ToDateTime(hebrewYear, month, day, 0, 0, 0, 0)),
            name,
            SpaceSpecialDayKind.Holiday,
            homeLeaveWeightMultiplier,
            RequiresCoverage: true,
            AlreadyExists: false);
}
