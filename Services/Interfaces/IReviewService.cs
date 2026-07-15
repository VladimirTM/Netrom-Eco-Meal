using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// CanReview is true once the signed-in customer has at least one completed order with the
// business and hasn't already left a review that would need updating instead.
public record ReviewContext(bool CanReview, Review? MyReview);

// Submitting is customer-only, and only for a business they've completed an order with.
public interface IReviewService
{
    public Task<List<Review>> GetAllAsync();
    public Task<List<Review>> GetByBusinessIdAsync(Guid businessId);
    public Task<List<Review>> GetByBusinessIdsAsync(IReadOnlyCollection<Guid> businessIds);
    public Task<ReviewContext> GetContextAsync(Guid businessId);
    public Task<Review> SubmitAsync(Guid businessId, int rating, string? comment);
}
