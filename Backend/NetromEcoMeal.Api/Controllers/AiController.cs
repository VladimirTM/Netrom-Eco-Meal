using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.DTOs;
using NetromEcoMeal.Models;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

// Actions can run minutes on CPU-only Ollama inference, so the Axios client and any reverse proxy
// need a matching long timeout. InvalidOperationException (middleware-mapped to 409) covers
// "AI isn't available" when Ollama isn't configured.
[Route("api/ai")]
[ApiController]
public class AiController(
    ISearchIntentParser searchIntentParser,
    IBasketPlannerAgent basketPlannerAgent,
    IPackageAiAssistant packageAiAssistant) : ControllerBase
{
    [HttpPost("search-intent")]
    [AllowAnonymous]
    public async Task<ActionResult<SearchIntent>> ParseSearchIntent([FromBody] ParseSearchIntentRequestDto request, CancellationToken cancellationToken) =>
        Ok(await searchIntentParser.ParseAsync(request.Utterance, request.PreviousIntent, cancellationToken));

    [HttpPost("basket-plan")]
    [Authorize]
    public async Task<ActionResult<BasketPlanDto>> ProposeBasket([FromBody] ProposeBasketRequestDto request, CancellationToken cancellationToken) =>
        Ok(BasketPlanDto.FromModel(await basketPlannerAgent.ProposeBasketAsync(request.PeopleCount, request.Budget, request.DietaryTag, cancellationToken)));

    [HttpPost("draft-description")]
    [Authorize]
    public async Task<ActionResult<string>> DraftDescription([FromBody] DraftDescriptionRequestDto request, CancellationToken cancellationToken) =>
        Ok(await packageAiAssistant.DraftDescriptionAsync(request.Name, request.PackageTypeName, request.DietaryTags, cancellationToken));
}
