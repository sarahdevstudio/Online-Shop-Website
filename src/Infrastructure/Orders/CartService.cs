using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Application.Orders;
using Shop.Domain.Orders;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Orders;

internal sealed class CartService(
    ShopDbContext db, IMediaStorage media, IOptions<CheckoutOptions> options, TimeProvider clock) : ICartService
{
    private CheckoutOptions Options => options.Value;

    public async Task<CartDto> GetAsync(string? token, CancellationToken ct)
    {
        var cart = await CartStore.FindAsync(db, token, clock, ct);
        if (cart is null)
        {
            return await BuildAsync(null, ct);
        }

        // Keep the 30-day window sliding without a write on every page view.
        var now = clock.GetUtcNow().UtcDateTime;
        if (cart.LastActivityAtUtc < now.AddDays(-1))
        {
            cart.Touch(now);
            await db.SaveChangesAsync(ct);
        }

        return await BuildAsync(cart, ct);
    }

    public async Task<int> CountAsync(string? token, CancellationToken ct)
    {
        if (!Tokens.LooksValid(token))
        {
            return 0;
        }

        var hash = Tokens.Hash(token!);
        var cutoff = CartStore.Cutoff(clock);
        return await db.Carts.AsNoTracking()
            .Where(c => c.TokenHash == hash && c.LastActivityAtUtc > cutoff)
            .SelectMany(c => c.Items)
            .SumAsync(i => (int?)i.Quantity, ct) ?? 0;
    }

    public async Task<Result<CartMutationDto>> AddItemAsync(string? token, AddCartItemRequest request, CancellationToken ct)
    {
        var infos = await VariantInfoLoader.LoadAsync(db, [request.VariantId], ct);
        if (!infos.TryGetValue(request.VariantId, out var info))
        {
            return Error.NotFound("variant_not_found", "این محصول پیدا نشد.");
        }

        if (!info.Purchasable)
        {
            return Error.Conflict("variant_unavailable", "این محصول در حال حاضر موجود نیست.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var cart = await CartStore.FindAsync(db, token, clock, ct);
        string? newToken = null;
        if (cart is null)
        {
            // Expired carts are purged when someone starts a new one; no background job needed.
            var cutoff = CartStore.Cutoff(clock);
            await db.Carts.Where(c => c.LastActivityAtUtc < cutoff).ExecuteDeleteAsync(ct);

            newToken = Tokens.New();
            cart = Cart.Create(Tokens.Hash(newToken), now);
            db.Carts.Add(cart);
        }

        var line = cart.Items.FirstOrDefault(i => i.VariantId == request.VariantId);
        if (line is null && cart.Items.Count >= Options.MaxCartLines)
        {
            return Error.Conflict("cart_full", "سبد خرید پر است.");
        }

        var max = info.MaxQuantity(Options);
        var wanted = (line?.Quantity ?? 0) + request.Quantity;
        var quantity = Math.Min(wanted, max);
        if (line is null)
        {
            cart.Items.Add(CartItem.Create(request.VariantId, quantity, info.Price));
        }
        else
        {
            line.Set(quantity, info.Price);
        }

        cart.Touch(now);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "UX_CartItems_CartId_VariantId"))
        {
            return Error.Conflict("concurrent_update", "سبد خرید هم‌زمان تغییر کرد. دوباره تلاش کنید.");
        }

        return new CartMutationDto(newToken, await BuildAsync(cart, ct), Clamped: wanted > quantity);
    }

    public async Task<Result<CartMutationDto>> SetQuantityAsync(string? token, int variantId, int quantity, CancellationToken ct)
    {
        var cart = await CartStore.FindAsync(db, token, clock, ct);
        var line = cart?.Items.FirstOrDefault(i => i.VariantId == variantId);
        if (cart is null || line is null)
        {
            return new CartMutationDto(null, await BuildAsync(cart, ct), Clamped: false);
        }

        var clamped = false;
        if (quantity <= 0)
        {
            cart.Items.Remove(line);
            db.CartItems.Remove(line);
        }
        else
        {
            var info = (await VariantInfoLoader.LoadAsync(db, [variantId], ct)).GetValueOrDefault(variantId);
            if (info is null || !info.Purchasable)
            {
                return Error.Conflict("variant_unavailable", "این محصول در حال حاضر موجود نیست.");
            }

            var max = info.MaxQuantity(Options);
            clamped = quantity > max;
            line.Set(Math.Min(quantity, max), info.Price);
        }

        cart.Touch(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return new CartMutationDto(null, await BuildAsync(cart, ct), clamped);
    }

    private async Task<CartDto> BuildAsync(Cart? cart, CancellationToken ct)
    {
        var items = cart?.Items.OrderBy(i => i.Id).ToList() ?? [];
        var infos = await VariantInfoLoader.LoadAsync(db, items.Select(i => i.VariantId).ToList(), ct);

        var lines = new List<CartLineDto>();
        foreach (var item in items)
        {
            if (!infos.TryGetValue(item.VariantId, out var info))
            {
                continue;
            }

            var max = info.MaxQuantity(Options);
            var notice = !info.Purchasable ? "unavailable"
                : item.Quantity > max ? "quantity_exceeds"
                : info.Price != item.SeenPrice ? "price_changed"
                : null;
            lines.Add(new CartLineDto(
                item.VariantId, info.ProductName, info.ProductSlug, info.Label, info.Sku,
                info.ImagePath is null ? null : media.GetUrl(info.ImagePath),
                info.Price, item.Quantity, max,
                info.Purchasable ? info.Price * item.Quantity : 0,
                info.Purchasable, notice,
                notice == "price_changed" ? item.SeenPrice : null));
        }

        var subtotal = lines.Sum(l => l.LineTotal);
        var shipping = OrderPricing.ShippingFee(subtotal, Options.ShippingFee, Options.FreeShippingThreshold);
        return new CartDto(
            lines, lines.Sum(l => l.Quantity), subtotal, shipping, subtotal + shipping,
            Options.FreeShippingThreshold,
            lines.Any(l => l.Notice is "unavailable" or "quantity_exceeds"),
            new PaymentMethodsDto(Options.CardToCard.Enabled, Options.CashOnDelivery.Enabled));
    }
}
