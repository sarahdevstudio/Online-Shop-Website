using System.Text.Json;

namespace Shop.Api.Infrastructure;

/// <summary>
/// Wraps every <c>/api/v1</c> JSON response body in <see cref="ResponseModel{T}"/>, success or error, while the
/// real HTTP status code stays exactly what the endpoint, filter or exception handler set. Installed once, early
/// in the pipeline (before routing, the exception handler and the status-code pages), so nothing downstream —
/// controller, minimal-API endpoint, filter, or <see cref="GlobalExceptionHandler"/> — needs to know the envelope
/// exists; this middleware rewrites whatever body they already produce.
/// </summary>
internal sealed class ResponseEnvelopeMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        // Nothing to wrap: HEAD, 204 No Content, 304 Not Modified, and the like.
        if (buffer.Length == 0 && context.Response.StatusCode < StatusCodes.Status400BadRequest)
        {
            return;
        }

        buffer.Position = 0;

        // A file download (the catalog export): a successful body that is not JSON is passed through as it is.
        if (context.Response.StatusCode < StatusCodes.Status400BadRequest
            && context.Response.ContentType is { } contentType && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
            return;
        }

        object envelope = context.Response.StatusCode < StatusCodes.Status400BadRequest
            ? new ResponseModel<JsonElement?> { Result = buffer.Length == 0 ? null : JsonSerializer.Deserialize<JsonElement>(buffer, JsonOptions) }
            : BuildErrorEnvelope(context.Response.StatusCode, buffer);

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = null;
        await JsonSerializer.SerializeAsync(originalBody, envelope, JsonOptions, context.RequestAborted);
    }

    // RFC 7807 fields that are never application data: everything else on the problem document (e.g. the stock
    // conflict's "lines"/"total"/"expectedTotal") is extra machine-readable detail, carried through as Result.
    private static readonly HashSet<string> WellKnownProblemFields = ["type", "title", "status", "detail", "instance", "traceId", "code", "errors"];

    private static ResponseModel<JsonElement?> BuildErrorEnvelope(int status, MemoryStream buffer)
    {
        string? message = null;
        string? code = null;
        IDictionary<string, string[]>? validationErrors = null;
        JsonElement? extensions = null;

        if (buffer.Length > 0)
        {
            using var document = JsonDocument.Parse(buffer);
            var root = document.RootElement;
            // "detail" is the specific, useful message when there is one (e.g. "the unpaid-order limit was reached");
            // "title" is only ever a generic per-status-type label, so it's the fallback, not the first choice.
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                message = detail.GetString();
            }

            if (message is null && root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                message = title.GetString();
            }

            if (root.TryGetProperty("code", out var codeProperty) && codeProperty.ValueKind == JsonValueKind.String)
            {
                code = codeProperty.GetString();
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                validationErrors = errors.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => property.Value.ValueKind == JsonValueKind.Array
                        ? property.Value.EnumerateArray().Select(v => v.GetString() ?? "").ToArray()
                        : []);
            }

            var extra = root.EnumerateObject().Where(p => !WellKnownProblemFields.Contains(p.Name)).ToList();
            if (extra.Count > 0)
            {
                using var extensionsDoc = JsonSerializer.SerializeToDocument(
                    extra.ToDictionary(p => p.Name, p => p.Value), JsonOptions);
                extensions = extensionsDoc.RootElement.Clone();
            }
        }

        return new ResponseModel<JsonElement?>
        {
            Result = extensions,
            ErrorMessage = message ?? ProblemText.ForStatus(status) ?? ProblemText.Unexpected,
            ErrorCode = string.IsNullOrEmpty(code) ? null : code,
            ValidationErrors = validationErrors,
        };
    }
}
