using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

[Route("api/reviews")]
[ApiController]
public class ReviewsController(IReviewService reviewService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReviewDto>>> GetAll() =>
        Ok((await reviewService.GetAllAsync()).Select(ReviewDto.FromEntity).ToList());

    [HttpGet("by-business/{businessId:guid}")]
    public async Task<ActionResult<List<ReviewDto>>> GetByBusiness(Guid businessId) =>
        Ok((await reviewService.GetByBusinessIdAsync(businessId)).Select(ReviewDto.FromEntity).ToList());

    [HttpGet("by-businesses")]
    public async Task<ActionResult<List<ReviewDto>>> GetByBusinesses([FromQuery] List<Guid> businessIds) =>
        Ok((await reviewService.GetByBusinessIdsAsync(businessIds)).Select(ReviewDto.FromEntity).ToList());

    [HttpGet("context/{businessId:guid}")]
    [Authorize]
    public async Task<ActionResult<ReviewContextDto>> GetContext(Guid businessId) =>
        Ok(ReviewContextDto.FromModel(await reviewService.GetContextAsync(businessId)));

    [HttpPost("{businessId:guid}")]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> Submit(Guid businessId, [FromBody] SubmitReviewRequestDto request) =>
        Ok(ReviewDto.FromEntity(await reviewService.SubmitAsync(businessId, request.Rating, request.Comment, request.PackageId)));
}
