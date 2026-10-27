using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Shop.Api.Controllers;

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

/// <summary>Marks an action as taking a multipart upload: any other content type is answered 415 before anything else runs.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireMultipartFormAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (!context.HttpContext.Request.HasFormContentType)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status415UnsupportedMediaType);
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
