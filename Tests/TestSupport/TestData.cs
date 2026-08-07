using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Tests.TestSupport;

// Fixed status IDs shared across tests, mirroring DbSeeder's convention of stable seed GUIDs.
public static class TestStatusIds
{
    public static readonly Guid Pending = new("99999999-0000-0000-0000-000000000001");
    public static readonly Guid Confirmed = new("99999999-0000-0000-0000-000000000002");
    public static readonly Guid Completed = new("99999999-0000-0000-0000-000000000003");
    public static readonly Guid Cancelled = new("99999999-0000-0000-0000-000000000004");
    public static readonly Guid NoShow = new("99999999-0000-0000-0000-000000000005");

    public static IEnumerable<Status> All() =>
    [
        new Status { Id = Pending, Name = OrderStatuses.Pending },
        new Status { Id = Confirmed, Name = OrderStatuses.Confirmed },
        new Status { Id = Completed, Name = OrderStatuses.Completed },
        new Status { Id = Cancelled, Name = OrderStatuses.Cancelled },
        new Status { Id = NoShow, Name = OrderStatuses.NoShow },
    ];

    public static Guid ForName(string name) => name switch
    {
        OrderStatuses.Pending => Pending,
        OrderStatuses.Confirmed => Confirmed,
        OrderStatuses.Completed => Completed,
        OrderStatuses.Cancelled => Cancelled,
        OrderStatuses.NoShow => NoShow,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };
}

public static class TestData
{
    public static ApplicationUser User(string id = "user-1", string name = "Test User") =>
        new() { Id = id, UserName = $"{id}@test.local", Email = $"{id}@test.local", Name = name };

    public static Business Business(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Test Business",
        Description = "A place that sells food",
        Address = "1 Test Street",
        BusinessTypeId = Guid.NewGuid(),
    };

    public static BusinessStaff BusinessStaff(Guid businessId, string userId) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        UserId = userId,
        AssignedAt = DateTime.UtcNow,
    };

    public static Package Package(Guid businessId, int quantity = 5, decimal weightKg = 1m) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        PackageTypeId = Guid.NewGuid(),
        Name = "Test Package",
        Description = "Some surplus food",
        Price = 10m,
        Quantity = quantity,
        WeightKg = weightKg,
        PickupStart = DateTime.UtcNow.AddHours(-1),
        PickupEnd = DateTime.UtcNow.AddHours(2),
    };

    // Not persisted through the mocked repository — build the full graph so ApplyStatusChangeAsync
    // can mutate order.OrderPackages[].Package.Quantity directly, same as the real code does.
    public static Order Order(ApplicationUser user, Guid businessId, string statusName, params (Package Package, int Quantity)[] lines)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            BusinessId = businessId,
            StatusId = TestStatusIds.ForName(statusName),
            Status = new Status { Id = TestStatusIds.ForName(statusName), Name = statusName },
            Business = Business(businessId),
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var (package, quantity) in lines)
        {
            order.OrderPackages.Add(new OrderPackage
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PackageId = package.Id,
                Package = package,
                Quantity = quantity,
            });
        }

        return order;
    }
}
