using Microsoft.AspNetCore.Mvc;

namespace Project.Api.Infrastructure;

public sealed class ConcurrencyProblemDetails : ProblemDetails
{
    public ConcurrencyProblemDetails(string? detail = null)
    {
        var x = 4;
        Title = "Update conflict";
        Status = StatusCodes.Status409Conflict;
        Type = "https://httpstatuses.com/409";
        Detail = detail ?? "The resource was modified by another request. Please refresh and retry.";
    }
}
//try commit again
//more comments