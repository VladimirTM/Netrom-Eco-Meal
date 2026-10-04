using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Repositories.Interfaces;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

// Same CSV shapes and the same admin-sees-everything / manager-scoped-to-their-own-business(es)
// rule regardless of caller. The frontend downloads through Axios (responseType: 'blob'), not a
// plain <a href>, since a JWT can't ride along on a browser-navigated link the way a cookie would.
[Route("api/orders")]
[ApiController]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.BusinessManager}")]
public class ExportsController(IOrderRepository orderRepository, IBusinessService businessService) : ControllerBase
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
