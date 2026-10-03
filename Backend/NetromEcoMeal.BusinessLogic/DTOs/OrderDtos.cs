using NetromEcoMeal.Entities;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.DTOs;

// WeightKg lets the frontend compute per-order/hero "kg saved" stats itself (Orders.razor's own
// formula: Completed orders' Sum(Quantity * Package.WeightKg)) without a dedicated endpoint.
// PickupStart/PickupEnd let it compute the same "widest span across all lines" pickup-window label
// Orders.razor/OrderPickupPass.razor show, without a dedicated endpoint either.
public record OrderLineDto(Guid PackageId, string PackageName, int Quantity, decimal UnitPrice, decimal WeightKg, DateTime PickupStart, DateTime PickupEnd);

public record PickupPassDto(Guid Id, string Label, DateTime CreatedAt, DateTime? RedeemedAt);

public record PaymentDto(Guid Id, decimal Amount, string Currency, string Status, DateTime CreatedAt, DateTime? RefundedAt);

public record OrderDto(
    Guid Id, int OrderNumber, Guid BusinessId, string BusinessName, string UserId, string CustomerName,
    string Status, DateTime CreatedAt, string? LogisticsNote, List<OrderLineDto> Lines, decimal Total,
    List<PickupPassDto> PickupPasses, PaymentDto? Payment)
{
    public static OrderDto FromEntity(Order o) => new(
        o.Id, o.OrderNumber, o.BusinessId, o.Business?.Name ?? "", o.UserId, o.User?.Name ?? "",
        o.Status?.Name ?? "", o.CreatedAt, o.LogisticsNote,
        o.OrderPackages.Select(op => new OrderLineDto(
            op.PackageId, op.Package?.Name ?? "", op.Quantity, op.Package?.Price ?? 0m, op.Package?.WeightKg ?? 0m,
            op.Package?.PickupStart ?? o.CreatedAt, op.Package?.PickupEnd ?? o.CreatedAt)).ToList(),
        o.OrderPackages.Sum(op => op.Quantity * (op.Package?.Price ?? 0m)),
        o.PickupPasses.Select(p => new PickupPassDto(p.Id, p.Label, p.CreatedAt, p.RedeemedAt)).ToList(),
        o.Payment is null ? null : new PaymentDto(o.Payment.Id, o.Payment.Amount, o.Payment.Currency, o.Payment.Status, o.Payment.CreatedAt, o.Payment.RefundedAt));
}

public record PlaceOrderRequestDto(Guid BusinessId, List<OrderLineRequest> Lines, string? LogisticsNote);

public record UpdateOrderStatusRequestDto(string StatusName);

public record SplitPickupPassesRequestDto(int PassCount);

public record StartCheckoutRequestDto(Guid BusinessId, List<OrderLineRequest> Lines, string? LogisticsNote);

public record CompleteCheckoutRequestDto(Guid PendingCheckoutId, string SessionId);

public record CheckoutCompletionResponseDto(bool Success, string Message, OrderDto? Order, decimal? KgSaved)
{
    public static CheckoutCompletionResponseDto FromModel(CheckoutCompletionResult result) =>
        new(result.Success, result.Message, result.Order is null ? null : OrderDto.FromEntity(result.Order), result.KgSaved);
}

public record StartRescueCircleRequestDto(Guid BusinessId, List<OrderLineRequest> Lines, int ParticipantCount, string? LogisticsNote);

public record CompleteRescueCircleShareRequestDto(Guid ParticipantId, string SessionId);

public record RescueCircleCompletionResponseDto(bool Success, string Message, RescueCircleSummary? Summary)
{
    public static RescueCircleCompletionResponseDto FromModel(RescueCircleCompletionResult result) =>
        new(result.Success, result.Message, result.Summary);
}

public record RescueCircleParticipantDto(string UserId, string UserName, bool IsOrganizer, decimal ShareAmount, DateTime JoinedAt, DateTime? PaidAt, DateTime? RefundedAt);

public record RescueCircleDetailDto(RescueCircleSummary Summary, string OrganizerId, List<RescueCircleParticipantDto> Participants);
