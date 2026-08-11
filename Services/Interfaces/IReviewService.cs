using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services.Interfaces;

// CanReview is true once the signed-in customer has a completed order with the business.
// ReviewablePackages is the picker source for optionally tagging the review to one they ordered.
public record ReviewContext(bool CanReview, Review? MyReview, List<Package> ReviewablePackages);

// Submitting is customer-only, and only for a business they've completed an order with.
public interface IReviewService
{
    public Task<List<Review>> GetAllAsync();
    public Task<List<Review>> GetByBusinessIdAsync(Guid businessId);
    public Task<List<Review>> GetByBusinessIdsAsync(IReadOnlyCollection<Guid> businessIds);
    public Task<ReviewContext> GetContextAsync(Guid businessId);
    public Task<Review> SubmitAsync(Guid businessId, int rating, string? comment, Guid? packageId = null);
}
