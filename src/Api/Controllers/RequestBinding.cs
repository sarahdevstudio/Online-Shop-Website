using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace Shop.Api.Controllers;

/// <summary>
/// Always "succeeds" with null, whatever the query string holds: for a <c>[FromQuery]</c> parameter that exists
/// only so the OpenAPI document keeps describing it (name and type), while the action itself reads and validates
/// the raw value through <see cref="QueryBinding"/>. Without this, MVC's own model binder would reject a value our
/// rules accept (or accept one they reject) and the automatic invalid-ModelState 400 would fire before the action's
/// own check ever runs.
/// </summary>
public sealed class DocumentationOnlyModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        // null for a reference/nullable-value type; the type's default (never null) for a plain value type.
        var value = bindingContext.ModelType.IsValueType && Nullable.GetUnderlyingType(bindingContext.ModelType) is null
            ? Activator.CreateInstance(bindingContext.ModelType)
            : null;
        bindingContext.Result = ModelBindingResult.Success(value);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Query-string binding with the rules the minimal API used, which differ from the MVC defaults: repeated values are
/// joined with commas, an empty value is not "absent", enum names are case-sensitive and numbers are accepted.
/// </summary>
public static class QueryBinding
{
    /// <summary>Absent = success with null; present = must parse, otherwise the caller answers a bare 400.</summary>
    public static bool TryGet(IQueryCollection query, string name, out int? value)
    {
        value = null;
        if (!query.TryGetValue(name, out var raw))
        {
            return true;
        }

        if (!int.TryParse(raw.ToString(), CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    public static bool TryGetEnum<T>(IQueryCollection query, string name, out T? value) where T : struct, Enum
    {
        value = null;
        if (!query.TryGetValue(name, out var raw))
        {
            return true;
        }

        if (!Enum.TryParse<T>(raw.ToString(), out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    public static string? GetString(IQueryCollection query, string name) =>
        query.TryGetValue(name, out StringValues raw) ? raw.ToString() : null;
}

/// <summary>
/// Marks an action as taking a multipart upload: a wrong content type is answered 415 before anything else runs.
/// A request with no body/content type at all falls through to the normal "required parameter missing" 400 — it
/// never declared a content type to be wrong about.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireMultipartFormAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.ContentType is not null && !request.HasFormContentType)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status415UnsupportedMediaType);
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
