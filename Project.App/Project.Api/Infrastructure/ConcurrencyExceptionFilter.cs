using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Project.Api.Infrastructure;

public sealed class ConcurrencyExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ConcurrencyExceptionFilter> _logger;
    public ConcurrencyExceptionFilter(ILogger<ConcurrencyExceptionFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Optimistic concurrency conflict detected.");

            var problem = new ConcurrencyProblemDetails(
                "The resource changed since you last fetched it. Fetch the latest version and try again.");

            context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
            context.ExceptionHandled = true; // prevent default 500
        }
    }
}
//try commit again
//more comments