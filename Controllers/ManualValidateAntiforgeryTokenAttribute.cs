using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Netrom_Eco_Meal.Controllers;

// [AutoValidateAntiforgeryToken] needs MVC's view-engine services (AddControllersWithViews/AddMvc),
// which this API-only project doesn't register. This validates the same token directly against
// IAntiforgery, which AddRazorComponents already wires up for Blazor's own forms.
public class ManualValidateAntiforgeryTokenAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestResult();
            return;
        }

        await next();
    }
}
