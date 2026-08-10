using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Models;

// Pure open/closed calculation for the Home.razor card badge and BusinessDetail.razor's hours
// panel — takes plain collections rather than an entity so it's testable without a DbContext.
public static class BusinessHoursStatus
{
    // Null means hours haven't been configured yet — callers should treat that as "unknown",
    // not "closed".
    public static bool? IsOpenNow(ICollection<BusinessHours> hours, ICollection<BusinessClosure> closures, DateTime localNow)
    {
        if (ActiveClosure(closures, localNow) is not null)
            return false;

        if (hours.Count == 0)
            return null;

        var today = hours.FirstOrDefault(h => h.DayOfWeek == localNow.DayOfWeek);
        if (today is null || today.IsClosed || today.OpenTime is null || today.CloseTime is null)
            return false;

        var timeNow = TimeOnly.FromDateTime(localNow);
        // Overnight windows (e.g. 22:00–02:00) close after midnight, so a simple >= open && <
        // close comparison would wrongly report "closed" for the whole stretch after midnight.
        return today.CloseTime > today.OpenTime
            ? timeNow >= today.OpenTime && timeNow < today.CloseTime
            : timeNow >= today.OpenTime || timeNow < today.CloseTime;
    }

    public static BusinessClosure? ActiveClosure(ICollection<BusinessClosure> closures, DateTime localNow)
    {
        var today = DateOnly.FromDateTime(localNow);
        return closures.FirstOrDefault(c => today >= c.StartDate && today <= c.EndDate);
    }
}
