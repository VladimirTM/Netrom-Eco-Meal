namespace NetromEcoMeal.Api.Middleware;

// The single exception-to-status mapping: services keep throwing UnauthorizedAccessException
// (403), InvalidOperationException for business-rule conflicts (409) and KeyNotFoundException
// (404), so controllers don't each need their own try/catch.
// StripeException is the same story for Payments/RescueCircles — a raw Stripe error (e.g. the
// basket total falling below Stripe's minimum chargeable amount) shouldn't crash the request;
// Message is deliberately not forwarded, since Stripe's wording isn't meant for an end user.
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, message) = ex switch
            {
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ex.Message),
                InvalidOperationException => (StatusCodes.Status409Conflict, ex.Message),
                KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                Stripe.StripeException => (StatusCodes.Status409Conflict,
                    "We couldn't process this payment request — please try a different basket total or try again shortly."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
            };

            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = message });
        }
    }
}
