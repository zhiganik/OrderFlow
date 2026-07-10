using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Order.Api.Filters;

// Validates the Idempotency-Key header before the action runs and stashes the
// value in HttpContext.Items, so the action never has to touch Request.Headers itself.
public sealed class RequireIdempotencyKeyAttribute : ActionFilterAttribute
{
    public const string HeaderName = "Idempotency-Key";
    public const string ContextItemKey = "IdempotencyKey";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var idempotencyKey) ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Title = "Idempotency-Key header is required.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            })
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
            return;
        }

        context.HttpContext.Items[ContextItemKey] = idempotencyKey.ToString();
    }
}
