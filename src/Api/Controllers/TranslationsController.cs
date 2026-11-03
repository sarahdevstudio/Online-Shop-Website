using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Translations;

namespace Shop.Api.Controllers;

// The storefront's texts (common.*, shop.*, seo.*): public, like the pages they appear on. api.* and admin.* are never served here.
[Route("api/v1/catalog/translations")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class TranslationsController : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] string? prefix)
    {
        Response.Headers.CacheControl = "public, max-age=30";
        return Http(Results.Ok(Translate.Current.Bundle(["common.", "shop.", "seo."], prefix)));
    }
}
