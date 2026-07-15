using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Repositories.Interfaces;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class ReviewService(
    IReviewRepository reviewRepository,
    IOrderRepository orderRepository,
    CurrentUserAccessor currentUser) : IReviewService
{
    public async Task<List<Review>> GetAllAsync()
    {
        return await reviewRepository.GetAllAsync();
    }

    public async Task<List<Review>> GetByBusinessIdAsync(Guid businessId)
    {
        return await reviewRepository.GetByBusinessIdAsync(businessId);
    }

    public async Task<List<Review>> GetByBusinessIdsAsync(IReadOnlyCollection<Guid> businessIds)
    {
        return await reviewRepository.GetByBusinessIdsAsync(businessIds);
    }

    public async Task<ReviewContext> GetContextAsync(Guid businessId)
    {
        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null || !await currentUser.IsInRoleAsync(AppRoles.Customer))
            return new ReviewContext(false, null);

        var myReview = await reviewRepository.GetByUserAndBusinessAsync(userId, businessId);
        var canReview = await HasCompletedOrderAsync(userId, businessId);

        return new ReviewContext(canReview, myReview);
    }

    public async Task<Review> SubmitAsync(Guid businessId, int rating, string? comment)
    {
        if (!await currentUser.IsInRoleAsync(AppRoles.Customer))
            throw new UnauthorizedAccessException("Only customers can leave reviews.");

        var (_, userId) = await currentUser.GetCurrentUserAsync();
        if (userId is null)
            throw new UnauthorizedAccessException("You must be signed in to leave a review.");

        if (rating is < 1 or > 5)
            throw new InvalidOperationException("Rating must be between 1 and 5.");

        if (!await HasCompletedOrderAsync(userId, businessId))
            throw new UnauthorizedAccessException("You can only review a business after completing an order with them.");

        var existing = await reviewRepository.GetByUserAndBusinessAsync(userId, businessId);
        if (existing is not null)
        {
            existing.Rating = rating;
            existing.Comment = comment;
            existing.CreatedAt = DateTime.UtcNow;
            await reviewRepository.SaveChangesAsync();
            return existing;
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            UserId = userId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTime.UtcNow,
        };

        await reviewRepository.AddAsync(review);
        await reviewRepository.SaveChangesAsync();
        return review;
    }

    private Task<bool> HasCompletedOrderAsync(string userId, Guid businessId) =>
        orderRepository.HasCompletedOrderAsync(userId, businessId);
}
