using System.Text.Json.Serialization;

namespace Project.Api.Utilities;

/// <summary>
/// Represents a standardized error response for the API.
/// Includes optional fields for validation errors and detailed messages.
/// </summary>
public record ApiExceptionResponse
{
    /// <summary>
    /// A short, human-readable summary of the problem type.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    public required int Status { get; init; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }

    /// <summary>
    /// An object containing validation errors. The keys are the names of the invalid fields.
    /// </summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    // --- implicit conversions from specific exceptions ---

    /// <summary>
    /// Implicitly converts a ValidationException into a standardized ApiError response.
    /// </summary>
    public static implicit operator ApiExceptionResponse(ValidationException ex) =>
        new()
        {
            Title = "Validation Error",
            Status = (int)ex.StatusCode,
            Errors = ex.Errors,
        };

    public static implicit operator ApiExceptionResponse(BadRequestException ex) =>
        new()
        {
            Title = "Bad Request",
            Status = (int)ex.StatusCode,
            Detail = ex.Message,
        };

    /// <summary>
    /// Implicitly converts a NotFoundException into a standardized ApiError response.
    /// </summary>
    public static implicit operator ApiExceptionResponse(NotFoundException ex) =>
        new()
        {
            Title = "Not Found",
            Status = (int)ex.StatusCode,
            Detail = ex.Message,
        };

    /// <summary>
    /// Implicitly converts a ConflictException into a standardized ApiError response.
    /// </summary>
    public static implicit operator ApiExceptionResponse(ConflictException ex) =>
        new()
        {
            Title = "Conflict",
            Status = (int)ex.StatusCode,
            Detail = ex.Message,
        };

    /// <summary>
    /// Provides a general fallback conversion for any other ApiException.
    /// </summary>
    public static implicit operator ApiExceptionResponse(ApiException ex) =>
        new()
        {
            Title = "API Error",
            Status = (int)ex.StatusCode,
            Detail = ex.Message,
            Errors = ex.Errors,
        };
}
