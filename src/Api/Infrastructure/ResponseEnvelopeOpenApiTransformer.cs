using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Shop.Api.Infrastructure;

/// <summary>
/// Wraps every 2xx and 4xx/5xx response schema in <see cref="ResponseModel{T}"/> so the generated OpenAPI document
/// reflects what clients actually receive at runtime (the <see cref="ResponseEnvelopeMiddleware"/> wraps every
/// /api/v1 response body in this envelope). Two component schemas are registered once:
/// <list type="bullet">
/// <item><c>ResponseModel</c> — the success shape (isSuccess=true, result holds the payload).</item>
/// <item><c>ResponseModelError</c> — the error shape (isSuccess=false, errorMessage populated).</item>
/// </list>
/// </summary>
internal sealed class ResponseEnvelopeOpenApiTransformer : IOpenApiDocumentTransformer
{
    private const string SuccessSchemaName = "ResponseModel";
    private const string ErrorSchemaName = "ResponseModelError";

    // OpenAPI 3.1 / JSON Schema uses a flags-style type for nullable (String | Null rather than a separate "nullable" field).
    private static readonly JsonSchemaType NullableString = JsonSchemaType.String | JsonSchemaType.Null;
    private static readonly JsonSchemaType NullableObject = JsonSchemaType.Object | JsonSchemaType.Null;

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        document.Components.Schemas.TryAdd(SuccessSchemaName, BuildSuccessSchema());
        document.Components.Schemas.TryAdd(ErrorSchemaName, BuildErrorSchema());

        var successRef = new OpenApiSchemaReference(SuccessSchemaName, document);
        var errorRef = new OpenApiSchemaReference(ErrorSchemaName, document);

        foreach (var pathItem in document.Paths!.Values.OfType<OpenApiPathItem>())
        {
            foreach (var operation in pathItem.Operations!.Values)
            {
                if (operation.Responses is null) continue;
                foreach (var (statusCode, responseItem) in operation.Responses)
                {
                    if (responseItem is not OpenApiResponse response) continue;
                    if (!int.TryParse(statusCode, out var code)) continue;

                    var schemaRef = code is >= 200 and < 300 ? successRef : errorRef;
                    response.Content ??= new Dictionary<string, OpenApiMediaType>();
                    response.Content["application/json"] = new OpenApiMediaType { Schema = schemaRef };
                }
            }
        }

        return Task.CompletedTask;
    }

    private static IOpenApiSchema BuildSuccessSchema() => new OpenApiSchema
    {
        Type = JsonSchemaType.Object,
        Description =
            "Every successful /api/v1 response is wrapped in this envelope. " +
            "\"result\" holds the actual payload (null for operations that return no data). " +
            "\"isSuccess\" is always true for 2xx responses.",
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["result"] = new OpenApiSchema
            {
                Description = "The operation payload; shape depends on the endpoint.",
            },
            ["successMessage"] = new OpenApiSchema { Type = NullableString, Description = "Optional Persian success message." },
            ["infoMessage"] = new OpenApiSchema { Type = NullableString },
            ["errorMessage"] = new OpenApiSchema { Type = NullableString },
            ["errorCode"] = new OpenApiSchema { Type = NullableString },
            ["message"] = new OpenApiSchema
            {
                Type = NullableString,
                Description = "Alias: equals successMessage on success, errorMessage on error.",
            },
            ["validationErrors"] = new OpenApiSchema
            {
                Type = NullableObject,
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            },
            ["isSuccess"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
            ["isError"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
        },
        Required = new HashSet<string> { "isSuccess", "isError" },
    };

    private static IOpenApiSchema BuildErrorSchema() => new OpenApiSchema
    {
        Type = JsonSchemaType.Object,
        Description =
            "Every error /api/v1 response is wrapped in this envelope. " +
            "\"errorMessage\" carries a human-readable Persian description. " +
            "\"errorCode\" is the stable machine code (e.g. \"product_not_found\"). " +
            "\"validationErrors\" is populated on 400 validation failures.",
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["result"] = new OpenApiSchema
            {
                Description = "Extra machine-readable details carried on some conflict responses.",
            },
            ["errorMessage"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Human-readable error in Persian." },
            ["errorCode"] = new OpenApiSchema { Type = NullableString },
            ["successMessage"] = new OpenApiSchema { Type = NullableString },
            ["infoMessage"] = new OpenApiSchema { Type = NullableString },
            ["message"] = new OpenApiSchema { Type = NullableString },
            ["validationErrors"] = new OpenApiSchema
            {
                Type = NullableObject,
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                },
                Description = "Field → [messages] map; populated for 400 validation failures.",
            },
            ["isSuccess"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
            ["isError"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
        },
        Required = new HashSet<string> { "errorMessage", "isSuccess", "isError" },
    };
}
