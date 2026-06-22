namespace Cheda.Core.Analytics;

public readonly record struct DateRange(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Contains(DateTimeOffset dt) => dt >= Start && dt < End;
    public double TotalDays => (End - Start).TotalDays;

    public static DateRange ForMonth(int year, int month, TimeSpan offset = default)
    {
        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, offset);
        return new(start, start.AddMonths(1));
    }

    public static DateRange ForWeek(DateTimeOffset anyDay)
    {
        var dow = (int)anyDay.LocalDateTime.DayOfWeek;
        var daysToMonday = dow == 0 ? -6 : 1 - dow;
        var monday = anyDay.LocalDateTime.Date.AddDays(daysToMonday);
        return new(
            new DateTimeOffset(monday, anyDay.Offset),
            new DateTimeOffset(monday.AddDays(7), anyDay.Offset));
    }
}
