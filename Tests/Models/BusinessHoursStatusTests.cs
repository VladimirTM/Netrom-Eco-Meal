using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Models;

namespace Netrom_Eco_Meal.Tests.Models;

// Pure-logic coverage for the open/closed calculation shared by Home.razor's card badge and
// BusinessDetail.razor's hours panel — no DbContext involved, just the Hours/Closures collections
// those pages already have loaded.
public class BusinessHoursStatusTests
{
    private static readonly Guid BusinessId = Guid.NewGuid();

    private static BusinessHours Hours(DayOfWeek day, TimeOnly? open, TimeOnly? close, bool isClosed = false) =>
        new() { Id = Guid.NewGuid(), BusinessId = BusinessId, DayOfWeek = day, IsClosed = isClosed, OpenTime = open, CloseTime = close };

    [Fact]
    public void IsOpenNow_NoHoursConfigured_ReturnsNull()
    {
        var result = BusinessHoursStatus.IsOpenNow([], [], new DateTime(2026, 8, 10, 12, 0, 0));

        Assert.Null(result);
    }

    [Fact]
    public void IsOpenNow_WithinTodaysWindow_ReturnsTrue()
    {
        // 2026-08-10 is a Monday.
        var hours = new List<BusinessHours> { Hours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)) };
        var now = new DateTime(2026, 8, 10, 12, 0, 0);

        Assert.True(BusinessHoursStatus.IsOpenNow(hours, [], now));
    }

    [Fact]
    public void IsOpenNow_OutsideTodaysWindow_ReturnsFalse()
    {
        var hours = new List<BusinessHours> { Hours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)) };
        var now = new DateTime(2026, 8, 10, 20, 0, 0);

        Assert.False(BusinessHoursStatus.IsOpenNow(hours, [], now));
    }

    [Fact]
    public void IsOpenNow_DayMarkedClosed_ReturnsFalse()
    {
        var hours = new List<BusinessHours> { Hours(DayOfWeek.Monday, null, null, isClosed: true) };
        var now = new DateTime(2026, 8, 10, 12, 0, 0);

        Assert.False(BusinessHoursStatus.IsOpenNow(hours, [], now));
    }

    [Fact]
    public void IsOpenNow_DayNotInHoursList_ReturnsFalse()
    {
        // Only Tuesday configured — Monday (2026-08-10) has no row at all.
        var hours = new List<BusinessHours> { Hours(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(18, 0)) };
        var now = new DateTime(2026, 8, 10, 12, 0, 0);

        Assert.False(BusinessHoursStatus.IsOpenNow(hours, [], now));
    }

    [Theory]
    [InlineData(23, 30, true)]  // 23:30 — within the overnight window
    [InlineData(1, 30, true)]   // 01:30 — past midnight, still within the window
    [InlineData(12, 0, false)]  // midday — well outside an overnight window
    public void IsOpenNow_OvernightWindow_WrapsPastMidnightCorrectly(int hour, int minute, bool expectedOpen)
    {
        // Open 22:00, close 02:00 — CloseTime < OpenTime signals an overnight wrap.
        var hours = new List<BusinessHours> { Hours(DayOfWeek.Monday, new TimeOnly(22, 0), new TimeOnly(2, 0)) };
        var now = new DateTime(2026, 8, 10, hour, minute, 0);

        Assert.Equal(expectedOpen, BusinessHoursStatus.IsOpenNow(hours, [], now));
    }

    [Fact]
    public void IsOpenNow_ActiveClosure_ReturnsFalseEvenDuringOpenHours()
    {
        var hours = new List<BusinessHours> { Hours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)) };
        var closures = new List<BusinessClosure>
        {
            new() { Id = Guid.NewGuid(), BusinessId = BusinessId, StartDate = new DateOnly(2026, 8, 9), EndDate = new DateOnly(2026, 8, 11), Reason = "Holiday" },
        };
        var now = new DateTime(2026, 8, 10, 12, 0, 0);

        Assert.False(BusinessHoursStatus.IsOpenNow(hours, closures, now));
    }

    [Fact]
    public void IsOpenNow_ClosureOutsideItsDateRange_DoesNotAffectResult()
    {
        var hours = new List<BusinessHours> { Hours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)) };
        var closures = new List<BusinessClosure>
        {
            new() { Id = Guid.NewGuid(), BusinessId = BusinessId, StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 7), Reason = "Future closure" },
        };
        var now = new DateTime(2026, 8, 10, 12, 0, 0);

        Assert.True(BusinessHoursStatus.IsOpenNow(hours, closures, now));
    }

    [Fact]
    public void ActiveClosure_DateWithinRange_ReturnsTheClosure()
    {
        var closure = new BusinessClosure { Id = Guid.NewGuid(), BusinessId = BusinessId, StartDate = new DateOnly(2026, 8, 9), EndDate = new DateOnly(2026, 8, 11), Reason = "Holiday" };
        var now = new DateTime(2026, 8, 10, 12, 0, 0);

        Assert.Same(closure, BusinessHoursStatus.ActiveClosure([closure], now));
    }

    [Fact]
    public void ActiveClosure_DateOutsideRange_ReturnsNull()
    {
        var closure = new BusinessClosure { Id = Guid.NewGuid(), BusinessId = BusinessId, StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 7), Reason = "Future" };
        var now = new DateTime(2026, 8, 10, 12, 0, 0);

        Assert.Null(BusinessHoursStatus.ActiveClosure([closure], now));
    }
}
