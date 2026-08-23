using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Controllers;

// A real HTTP endpoint, unlike the other Controllers (DI'd into Razor pages) — reads identity
// off HttpContext.User since there's no Blazor circuit here for CurrentUserAccessor to use.
[ApiController]
[Route("api/orders")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.BusinessManager}")]
public class OrderExportController(IOrderRepository orderRepository, IBusinessService businessService) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsvAsync(DateTime? from, DateTime? to, Guid? businessId = null)
    {
        var (effectiveBusinessId, error) = await ResolveEffectiveBusinessIdAsync(businessId);
        if (error is not null)
            return error;

        var orders = await orderRepository.GetInRangeAsync(effectiveBusinessId, from, to);

        var csv = BuildCsv(orders);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"orders-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // Same scoping/auth rules as ExportCsvAsync above, but shaped for the payout ledger on
    // /payments (payment status/dates) rather than order fulfillment.
    [HttpGet("/api/payments/export")]
    public async Task<IActionResult> ExportPaymentsCsvAsync(DateTime? from, DateTime? to, Guid? businessId = null)
    {
        var (effectiveBusinessId, error) = await ResolveEffectiveBusinessIdAsync(businessId);
        if (error is not null)
            return error;

        var orders = await orderRepository.GetInRangeAsync(effectiveBusinessId, from, to);

        var csv = BuildPaymentsCsv(orders);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"payments-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // Admin: trusts whatever businessId was passed (null = every business). Manager: scoped to
    // whichever business(es) they actually staff — an explicit businessId must be one of theirs,
    // and staffing more than one without specifying which is ambiguous rather than silently "all".
    private async Task<(Guid? businessId, IActionResult? error)> ResolveEffectiveBusinessIdAsync(Guid? businessId)
    {
        if (User.IsInRole(AppRoles.Admin))
            return (businessId, null);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var staffBusinesses = userId is null ? [] : await businessService.GetByStaffUserIdAsync(userId);
        if (staffBusinesses.Count == 0)
            return (null, Unauthorized());

        if (businessId is not null)
            return staffBusinesses.All(b => b.Id != businessId) ? (null, Forbid()) : (businessId, null);

        if (staffBusinesses.Count == 1)
            return (staffBusinesses[0].Id, null);

        return (null, BadRequest("You manage more than one business — specify businessId."));
    }

    private static string BuildCsv(List<Order> orders)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Order Number,Business,Customer,Status,Placed At (UTC),Items,Total,Kg Saved");

        foreach (var order in orders)
        {
            var items = string.Join("; ", order.OrderPackages.Select(op => $"{op.Quantity}x {op.Package.Name}"));
            var total = order.OrderPackages.Sum(op => op.Quantity * op.Package.Price);
            var kgSaved = order.Status.Name == OrderStatuses.Completed
                ? order.OrderPackages.Sum(op => op.Quantity * op.Package.WeightKg)
                : 0m;

            sb.AppendLine(string.Join(",",
                Csv(order.OrderNumber.ToString("000")),
                Csv(order.Business.Name),
                Csv(order.User.Name),
                Csv(order.Status.Name),
                Csv(order.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                Csv(items),
                Csv(total.ToString("0.00", CultureInfo.InvariantCulture)),
                Csv(kgSaved.ToString("0.00", CultureInfo.InvariantCulture))));
        }

        return sb.ToString();
    }

    private static string BuildPaymentsCsv(List<Order> orders)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Order Number,Business,Customer,Amount,Currency,Payment Status,Paid At (UTC),Refunded At (UTC)");

        foreach (var order in orders.Where(o => o.Payment is not null))
        {
            var payment = order.Payment!;

            sb.AppendLine(string.Join(",",
                Csv(order.OrderNumber.ToString("000")),
                Csv(order.Business.Name),
                Csv(order.User.Name),
                Csv(payment.Amount.ToString("0.00", CultureInfo.InvariantCulture)),
                Csv(payment.Currency.ToUpperInvariant()),
                Csv(PaymentStatuses.Label(payment.Status)),
                Csv(payment.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                Csv(payment.RefundedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "")));
        }

        return sb.ToString();
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
