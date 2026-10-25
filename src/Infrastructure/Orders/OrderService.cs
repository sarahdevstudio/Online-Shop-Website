using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Application.Orders;
using Shop.Application.Settings;
using Shop.Domain.Common;
using Shop.Domain.Orders;
using Shop.Domain.Settings;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Orders;

internal sealed class OrderService(
    ShopDbContext db, IStockService stock, IStoreSettings settings, IOptions<CheckoutOptions> options, TimeProvider clock) : IOrderService
{
    private const string UnknownIp = "unknown";

    private CheckoutOptions Options => options.Value;

    public async Task<Result<PlacedOrderDto>> PlaceAsync(PlaceOrderRequest request, PlaceOrderContext context, CancellationToken ct)
    {
        // One snapshot of the settings for this whole request: the price, the limits and the card details shown on the
        // order all come from the same moment.
        var store = await settings.GetAsync(ct);
        if (!store.IsOffered(request.PaymentMethod))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["paymentMethod"] = ["روش پرداخت انتخاب‌شده فعال نیست."] });
        }

        if (!IranianMobile.TryNormalize(request.Mobile, out var mobile) || !PostalCodes.TryNormalize(request.PostalCode, out var postalCode))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["mobile"] = ["شماره موبایل یا کد پستی معتبر نیست."] });
        }

        var ip = string.IsNullOrWhiteSpace(context.ClientIp) ? UnknownIp : context.ClientIp;

        try
        {
            return await PlaceCoreAsync(request, context, store, mobile, postalCode, ip, ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "UX_Orders_IdempotencyKey"))
        {
            // Defensive: the advisory lock below already serialises equal keys.
            db.ChangeTracker.Clear();
            var existing = await ReplayAsync(context.IdempotencyKey, ct);
            return existing is not null
                ? existing
                : Error.Conflict("concurrent_update", "سفارش هم‌زمان ثبت شد. دوباره تلاش کنید.");
        }
    }

    private async Task<Result<PlacedOrderDto>> PlaceCoreAsync(
        PlaceOrderRequest request, PlaceOrderContext context, StoreSettingsValues store, string mobile, string postalCode, string ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Lock order is fixed (key, ip, mobile) so concurrent checkouts can never deadlock on these. Equal idempotency keys
        // serialise on the first lock, so the second one finds the first one's order instead of racing for the stock.
        await LockAsync($"order-key:{context.IdempotencyKey:N}", ct);
        var replay = await ReplayAsync(context.IdempotencyKey, ct);
        if (replay is not null)
        {
            return replay;
        }

        var cart = await CartStore.FindAsync(db, context.CartToken, clock, ct);
        if (cart is null || cart.Items.Count == 0)
        {
            return Error.Conflict("cart_empty", "سبد خرید شما خالی است.");
        }

        if (ip != UnknownIp)
        {
            await LockAsync($"order-ip:{ip}", ct);
        }

        await LockAsync($"order-mobile:{mobile}", ct);

        var limit = await CheckUnpaidLimitAsync(mobile, ip, ct);
        if (limit is not null)
        {
            return limit;
        }

        // Variants in id order: stock rows are always locked in the same order.
        var lines = cart.Items.OrderBy(i => i.VariantId).ToList();
        var infos = await VariantInfoLoader.LoadAsync(db, lines.Select(l => l.VariantId).ToList(), ct);

        var shortages = Shortages(lines, infos, store.MaxQuantityPerLine);
        if (shortages.Count > 0)
        {
            return StockError(shortages);
        }

        var subtotal = lines.Sum(l => infos[l.VariantId].Price * l.Quantity);
        var shipping = OrderPricing.ShippingFee(subtotal, store.ShippingFlatFee, store.FreeShippingThreshold);
        if (subtotal + shipping != request.ExpectedTotal)
        {
            return Error.Conflict(
                "price_changed",
                "قیمت یا هزینه ارسال تغییر کرده است. سبد خرید را دوباره بررسی کنید.",
                new Dictionary<string, object?> { ["total"] = subtotal + shipping, ["expectedTotal"] = request.ExpectedTotal });
        }

        foreach (var line in lines)
        {
            if (!await stock.TryDecrementAsync(line.VariantId, line.Quantity, ct))
            {
                // Someone bought it between the read and the update. Roll everything back and tell the customer what is short.
                await tx.RollbackAsync(ct);
                var current = await VariantInfoLoader.LoadAsync(db, lines.Select(l => l.VariantId).ToList(), ct);
                var failed = Shortages(lines, current, store.MaxQuantityPerLine);
                if (failed.All(s => s.VariantId != line.VariantId))
                {
                    var info = current[line.VariantId];
                    failed.Add(new StockShortageDto(line.VariantId, info.ProductName, info.Label, line.Quantity, info.Purchasable ? info.Stock : 0));
                }

                return StockError(failed);
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var items = lines.Select(l =>
        {
            var info = infos[l.VariantId];
            return OrderItem.Create(l.VariantId, info.ProductName, info.Label, info.Sku, info.Price, l.Quantity);
        });
        var order = Order.Create(
            Tokens.New(), context.IdempotencyKey, request.PaymentMethod,
            PersianText.Normalize(request.FullName), mobile,
            PersianText.Normalize(request.Province), PersianText.Normalize(request.City),
            PersianText.NormalizeLetters(request.Address), postalCode,
            request.Note is null ? null : PersianText.NormalizeLetters(request.Note),
            ip, subtotal, shipping,
            request.PaymentMethod == PaymentMethod.CardToCard ? new CardSnapshot(store.CardNumber, store.CardHolderName, store.BankName) : null,
            items, now);

        db.Orders.Add(order);
        db.Carts.Remove(cart);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PlacedOrderDto(order.AccessToken, order.Number, Replayed: false);
    }

    public async Task<Result<OrderDto>> GetAsync(string accessToken, CancellationToken ct)
    {
        var order = await FindByTokenAsync(accessToken, track: false, ct);
        return order is null ? Error.NotFound("order_not_found", "سفارش پیدا نشد.") : OrderMapper.ToDto(order);
    }

    public async Task<Result<OrderDto>> SubmitTransferReferenceAsync(string accessToken, TransferReferenceRequest request, CancellationToken ct)
    {
        var order = await FindByTokenAsync(accessToken, track: true, ct);
        if (order is null)
        {
            return Error.NotFound("order_not_found", "سفارش پیدا نشد.");
        }

        var tracking = PersianText.Normalize(Digits.ToLatin(request.Tracking));
        var time = PersianText.Normalize(Digits.ToLatin(request.Time));
        var last4 = Digits.ToLatin(request.CardLast4);
        if (!order.TrySetTransferReference(tracking, time, last4, clock.GetUtcNow().UtcDateTime))
        {
            return Error.Conflict("not_awaiting_payment", "این سفارش در انتظار پرداخت کارت‌به‌کارت نیست.");
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("concurrent_update", "سفارش هم‌زمان تغییر کرد. صفحه را تازه کنید.");
        }

        return OrderMapper.ToDto(order);
    }

    private async Task<PlacedOrderDto?> ReplayAsync(Guid key, CancellationToken ct) =>
        await db.Orders.AsNoTracking()
            .Where(o => o.IdempotencyKey == key)
            .Select(o => new PlacedOrderDto(o.AccessToken, o.Number, true))
            .FirstOrDefaultAsync(ct);

    private Task<Order?> FindByTokenAsync(string accessToken, bool track, CancellationToken ct)
    {
        if (!Tokens.LooksValid(accessToken))
        {
            return Task.FromResult<Order?>(null);
        }

        var query = db.Orders.Include(o => o.Items).AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(o => o.AccessToken == accessToken, ct);
    }

    private async Task<Error?> CheckUnpaidLimitAsync(string mobile, string ip, CancellationToken ct)
    {
        var unpaid = Order.UnpaidStatuses;
        var byMobile = await db.Orders.CountAsync(o => o.Mobile == mobile && unpaid.Contains(o.Status), ct);
        var byIp = ip == UnknownIp ? 0 : await db.Orders.CountAsync(o => o.ClientIp == ip && unpaid.Contains(o.Status), ct);

        var limit = byMobile >= Options.MaxUnpaidOrdersPerMobile ? Options.MaxUnpaidOrdersPerMobile
            : byIp >= Options.MaxUnpaidOrdersPerIp ? Options.MaxUnpaidOrdersPerIp
            : 0;
        if (limit == 0)
        {
            return null;
        }

        return Error.Conflict(
            "unpaid_limit",
            $"شما هم‌اکنون {PersianDigits(limit)} سفارش پرداخت‌نشده دارید. برای ثبت سفارش جدید، ابتدا سفارش‌های قبلی را پرداخت یا لغو کنید.",
            new Dictionary<string, object?> { ["limit"] = limit });
    }

    /// <summary>Lines that cannot be bought in the requested quantity right now.</summary>
    private static List<StockShortageDto> Shortages(IReadOnlyList<CartItem> lines, IReadOnlyDictionary<int, VariantInfo> infos, int maxPerLine)
    {
        var result = new List<StockShortageDto>();
        foreach (var line in lines)
        {
            if (!infos.TryGetValue(line.VariantId, out var info))
            {
                result.Add(new StockShortageDto(line.VariantId, "؟", null, line.Quantity, 0));
            }
            else if (line.Quantity > info.MaxQuantity(maxPerLine))
            {
                result.Add(new StockShortageDto(line.VariantId, info.ProductName, info.Label, line.Quantity, info.MaxQuantity(maxPerLine)));
            }
        }

        return result;
    }

    private static Error StockError(List<StockShortageDto> shortages) =>
        Error.Conflict(
            "stock_insufficient",
            "موجودی برخی کالاها کافی نیست. سفارش ثبت نشد.",
            new Dictionary<string, object?> { ["lines"] = shortages });

    private Task LockAsync(string resource, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DECLARE @result int;
             EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
             IF @result < 0 THROW 50001, 'Could not acquire the order lock.', 1;
             """,
            ct);

    private static string PersianDigits(int value) =>
        string.Concat(value.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(c => (char)('۰' + (c - '0'))));
}

internal static class OrderMapper
{
    public static OrderDto ToDto(Order order) => new(
        order.Number, order.Status, order.PaymentStatus, order.PaymentMethod, order.CreatedAtUtc,
        order.FullName, order.Mobile, order.Province, order.City, order.Address, order.PostalCode, order.Note,
        order.Items.OrderBy(i => i.Id)
            .Select(i => new OrderLineDto(i.ProductName, i.VariantLabel, i.Sku, i.UnitPrice, i.Quantity, i.LineTotal))
            .ToList(),
        order.Subtotal, order.ShippingFee, order.Total,
        // The order's own snapshot, never the current settings: changing the card later must not alter existing orders.
        order is { PaymentMethod: PaymentMethod.CardToCard, Status: OrderStatus.AwaitingPayment, PaymentCardNumber: not null }
            ? new PaymentInstructionsDto(order.PaymentCardNumber, order.PaymentCardHolder ?? string.Empty, order.PaymentBankName ?? string.Empty, order.Total)
            : null,
        order.TransferTracking is null
            ? null
            : new TransferReferenceDto(order.TransferTracking, order.TransferTime ?? string.Empty, order.TransferCardLast4 ?? string.Empty));
}
