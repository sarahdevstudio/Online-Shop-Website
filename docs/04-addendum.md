# Addendum to the approved ADR

Decisions made after the ADR (01-03 are copied from the legacy repository for reference).

1. **Payment method:** the owner chose card-to-card and pay-in-store, not an online gateway. Orders (Phase 7) therefore use
   manual confirmation by an admin with `Payment` / `PaymentTransaction` as the audit trail. The `IPaymentGateway` port and
   `FakePaymentGateway` exist (Phase 1) so an online provider can be added later without touching the order domain.
2. **Location:** this project lives in its own repository at `D:\src\os\shop-mvp`, outside the legacy iCMSCore repository.
3. **.NET 10 (LTS)** SDK 10.0.401 installed on the dev machine; `global.json` pins SDK >= 10.0.100.
4. **Phase 1 deviation:** identity types (`ApplicationUser`, `ApplicationRole`) live in `Shop.Infrastructure`, not in
   `Shop.Domain`, to keep the domain free of ASP.NET Identity. The domain holds `Customer`, `Roles` and `Permissions`.
5. **Email confirmation** is deferred until an email sender exists (outbox, Phase 7).
6. **Store domain (2026-10-06):** the store sells lamps and chandeliers (لامپ و لوستر). The design system (Phase 2.5) is themed
   for it: warm brass/amber primary on warm neutrals (`web/projects/shared-ui/src/styles/tokens.css`, AA contrast verified in both
   themes), lighting sample catalogue and navigation in the style guide. Phase 3 product pages must expose lighting attributes
   (wattage, socket such as E27/E14/GU10, colour temperature, lumens, dimmable, number of arms, bulb included, dimensions/ceiling
   height, voltage) through the existing Attribute/Option model, and the shipping rules need fragile-goods handling.
