using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Application.Staff;
using Shop.Domain.Common;
using Shop.Domain.Security;
using Shop.Infrastructure.Accounts;
using Shop.Infrastructure.Identity;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Staff;

/// <summary>
/// Staff accounts and roles (docs/16). Every change is written to the staff audit log. The rule that matters most: after any
/// change at least one ACTIVE user must still hold every permission, otherwise nobody could repair the roles again; a change
/// that would break it is rolled back with a conflict. Role and permission changes, and password resets, ask for the caller's
/// own password again (a wrong one counts as a failed sign-in).
/// </summary>
internal sealed class StaffService(
    ShopDbContext db, UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles, ICurrentUser currentUser, TimeProvider clock) : IStaffService
{
    private const int PageSize = 50;
    public const int DisplayNameMaxLength = 128;
    public const int RoleTitleMaxLength = 80;
    public const int RoleNameMinLength = 3;
    public const int RoleNameMaxLength = 40;

    private static readonly Error NotFound = Error.NotFound("staff_not_found", "api.error.staff-not-found");
    private static readonly Error RoleNotFound = Error.NotFound("role_not_found", "api.error.role-not-found");
    private static readonly Error Stale = Error.Conflict("concurrent_update", "api.error.staff-concurrent-update");
    private static readonly Error Lockout = Error.Conflict("staff_lockout", "api.error.staff-lockout");

    // ---- reading -------------------------------------------------------------------------------------------------------

    public async Task<StaffOverviewDto> GetOverviewAsync(CancellationToken ct)
    {
        var roleRows = await LoadRolesAsync(ct);
        var staff = await LoadStaffAsync(null, ct);
        return new StaffOverviewDto(staff, roleRows, Permissions.All);
    }

    private async Task<List<RoleDto>> LoadRolesAsync(CancellationToken ct)
    {
        var rows = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var claims = await db.RoleClaims.AsNoTracking().Where(c => c.ClaimType == Permissions.ClaimType)
            .Select(c => new { c.RoleId, c.ClaimValue }).ToListAsync(ct);
        var counts = await db.UserRoles.AsNoTracking().GroupBy(ur => ur.RoleId).Select(g => new { RoleId = g.Key, Count = g.Count() }).ToListAsync(ct);
        return rows.Select(r => new RoleDto(
            r.Id, r.Name!, r.Title, Roles.All.Contains(r.Name!),
            claims.Where(c => c.RoleId == r.Id).Select(c => c.ClaimValue!).Order(StringComparer.Ordinal).ToList(),
            counts.FirstOrDefault(c => c.RoleId == r.Id)?.Count ?? 0, r.ConcurrencyStamp ?? string.Empty)).ToList();
    }

    private async Task<List<StaffUserDto>> LoadStaffAsync(Guid? only, CancellationToken ct)
    {
        var rows = await (from u in db.Users.AsNoTracking()
                          join ur in db.UserRoles.AsNoTracking() on u.Id equals ur.UserId
                          join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                          where r.Name != Roles.Customer && (only == null || u.Id == only)
                          select new { User = u, Role = r.Name! }).ToListAsync(ct);
        return rows.GroupBy(x => x.User.Id).Select(g =>
        {
            var u = g.First().User;
            return new StaffUserDto(u.Id, u.UserName!, u.DisplayName, u.Email, u.IsActive, g.Select(x => x.Role).Order(StringComparer.Ordinal).ToList(), u.CreatedAtUtc, u.ConcurrencyStamp ?? string.Empty);
        }).OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<StaffUserDto> ToDtoAsync(Guid id, CancellationToken ct) => (await LoadStaffAsync(id, ct)).Single();

    // ---- accounts --------------------------------------------------------------------------------------------------------

    public async Task<Result<StaffUserDto>> CreateAsync(CreateStaffRequest request, CancellationToken ct)
    {
        var roleNames = (request.Roles ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (await CheckRolesAsync(roleNames, "roles", ct) is { } roleProblem)
        {
            return roleProblem;
        }

        var displayName = Clean(request.DisplayName);
        if (displayName is { Length: > DisplayNameMaxLength })
        {
            return TooLong("displayName", DisplayNameMaxLength);
        }

        // Creating an account hands out permissions: same step-up as changing roles.
        if (await StepUpAsync(request.CurrentPassword, "create", ct) is { } denied)
        {
            return denied;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var created = await AdminAccounts.CreateAdminAsync(users, request.Username ?? string.Empty, request.Password ?? string.Empty, clock, request.Email, displayName, roleNames);
        if (!created.IsSuccess)
        {
            return created.Error!;
        }

        var user = (await users.FindByNameAsync(request.Username!.Trim()))!;
        Audit(StaffAuditActions.Created, user.UserName, "roles=" + string.Join(",", roleNames));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ToDtoAsync(user.Id, ct);
    }

    public async Task<Result<StaffUserDto>> UpdateAsync(Guid id, UpdateStaffRequest request, CancellationToken ct)
    {
        var user = await FindStaffAsync(id);
        if (user is null)
        {
            return NotFound;
        }

        if (user.ConcurrencyStamp != request.Stamp)
        {
            return Stale;
        }

        var displayName = Clean(request.DisplayName);
        if (displayName is { Length: > DisplayNameMaxLength })
        {
            return TooLong("displayName", DisplayNameMaxLength);
        }

        var email = Clean(request.Email);
        if (email is not null && (email.Length > 256 || !email.Contains('@') || email.Contains(' ')))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["email"] = ["api.validation.email-valid"] });
        }

        if (email is not null && !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase)
            && await users.FindByEmailAsync(email) is { } other && other.Id != user.Id)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["email"] = ["api.error.staff-email-exists"] });
        }

        var changed = new List<string>();
        if (displayName != user.DisplayName)
        {
            user.DisplayName = displayName;
            changed.Add("displayName");
        }

        if (!string.Equals(email, user.Email, StringComparison.Ordinal))
        {
            user.Email = email;
            user.NormalizedEmail = email is null ? null : users.NormalizeEmail(email);
            user.EmailConfirmed = email is not null;
            changed.Add("email");
        }

        if (changed.Count > 0)
        {
            var updated = await users.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                return Stale;
            }

            Audit(StaffAuditActions.Updated, user.UserName, string.Join(",", changed));
            await db.SaveChangesAsync(ct);
        }

        return await ToDtoAsync(id, ct);
    }

    public async Task<Result<StaffUserDto>> SetRolesAsync(Guid id, SetStaffRolesRequest request, CancellationToken ct)
    {
        var user = await FindStaffAsync(id);
        if (user is null)
        {
            return NotFound;
        }

        var wanted = (request.Roles ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (wanted.Count == 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["roles"] = ["api.validation.staff-role-required"] });
        }

        if (await CheckRolesAsync(wanted, "roles", ct) is { } roleProblem)
        {
            return roleProblem;
        }

        if (user.ConcurrencyStamp != request.Stamp)
        {
            return Stale;
        }

        if (await StepUpAsync(request.CurrentPassword, "roles", ct) is { } denied)
        {
            return denied;
        }

        var current = (await users.GetRolesAsync(user)).ToList();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var customerRole = current.Where(r => r == Roles.Customer).ToList(); // a staff account that is also a shopper keeps that
        var toRemove = current.Except(wanted).Except(customerRole).ToList();
        var toAdd = wanted.Except(current).ToList();
        if (toRemove.Count > 0)
        {
            await users.RemoveFromRolesAsync(user, toRemove);
        }

        if (toAdd.Count > 0)
        {
            await users.AddToRolesAsync(user, toAdd);
        }

        // Permissions are read when the session is created: a new security stamp makes the person sign in again with the new set.
        await users.UpdateSecurityStampAsync(user);
        Audit(StaffAuditActions.RolesChanged, user.UserName, $"{string.Join(",", current)} -> {string.Join(",", wanted)}");
        await db.SaveChangesAsync(ct);
        if (!await SomeoneKeepsEverythingAsync(ct))
        {
            await tx.RollbackAsync(ct);
            return Lockout;
        }

        await tx.CommitAsync(ct);
        return await ToDtoAsync(id, ct);
    }

    public async Task<Result<StaffUserDto>> SetActiveAsync(Guid id, SetStaffActiveRequest request, CancellationToken ct)
    {
        var user = await FindStaffAsync(id);
        if (user is null)
        {
            return NotFound;
        }

        if (user.ConcurrencyStamp != request.Stamp)
        {
            return Stale;
        }

        if (!request.IsActive && user.Id == currentUser.UserId)
        {
            return Error.Conflict("staff_self_deactivate", "api.error.staff-self-deactivate");
        }

        if (user.IsActive == request.IsActive)
        {
            return await ToDtoAsync(id, ct);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        user.IsActive = request.IsActive;
        await users.UpdateAsync(user);
        if (!request.IsActive)
        {
            await users.UpdateSecurityStampAsync(user); // every session of the person ends with the next request
        }

        Audit(request.IsActive ? StaffAuditActions.Activated : StaffAuditActions.Deactivated, user.UserName, null);
        await db.SaveChangesAsync(ct);
        if (!await SomeoneKeepsEverythingAsync(ct))
        {
            await tx.RollbackAsync(ct);
            return Lockout;
        }

        await tx.CommitAsync(ct);
        return await ToDtoAsync(id, ct);
    }

    public async Task<Result<StaffUserDto>> ResetPasswordAsync(Guid id, ResetStaffPasswordRequest request, CancellationToken ct)
    {
        var user = await FindStaffAsync(id);
        if (user is null)
        {
            return NotFound;
        }

        if (user.ConcurrencyStamp != request.Stamp)
        {
            return Stale;
        }

        if (AdminPasswordRules.Weakness(request.NewPassword, user.UserName) is { } weakness)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["newPassword"] = [weakness] });
        }

        if (await StepUpAsync(request.CurrentPassword, "password", ct) is { } denied)
        {
            return denied;
        }

        // No reset-token provider is registered (and none is needed: the caller is an authorised admin): replace the hash directly.
        var removed = await users.RemovePasswordAsync(user);
        var reset = removed.Succeeded ? await users.AddPasswordAsync(user, request.NewPassword) : removed;
        if (!reset.Succeeded)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["newPassword"] = reset.Errors.Select(e => e.Code).ToArray() });
        }

        await users.SetLockoutEndDateAsync(user, null); // also lifts a lockout from too many wrong passwords
        await users.ResetAccessFailedCountAsync(user);
        await users.UpdateSecurityStampAsync(user); // the old password's sessions end
        Audit(StaffAuditActions.PasswordReset, user.UserName, null);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(id, ct);
    }

    // ---- roles ---------------------------------------------------------------------------------------------------------

    public async Task<Result<RoleDto>> SaveRoleAsync(Guid? id, SaveRoleRequest request, CancellationToken ct)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var title = Clean(request.Title);
        var errors = new Dictionary<string, string[]>();
        if (!IsValidRoleName(name))
        {
            errors["name"] = [Msg.Of("api.validation.role-name-format", ("min", RoleNameMinLength), ("max", RoleNameMaxLength))];
        }

        if (title is null)
        {
            errors["title"] = ["api.validation.role-title-required"];
        }
        else if (title.Length > RoleTitleMaxLength)
        {
            errors["title"] = [Msg.Of("api.validation.text-max-length", ("max", RoleTitleMaxLength))];
        }

        var permissions = (request.Permissions ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (permissions.Any(p => !Permissions.All.Contains(p)))
        {
            errors["permissions"] = ["api.validation.role-permission-unknown"];
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        ApplicationRole? role = null;
        if (id is { } roleId)
        {
            role = await roles.FindByIdAsync(roleId.ToString());
            if (role is null)
            {
                return RoleNotFound;
            }

            if (Roles.All.Contains(role.Name!))
            {
                return Error.Conflict("role_system", "api.error.role-system");
            }

            if (role.ConcurrencyStamp != request.Stamp)
            {
                return Stale;
            }
        }

        if (Roles.All.Contains(name, StringComparer.OrdinalIgnoreCase)
            || (await roles.FindByNameAsync(name) is { } sameName && sameName.Id != role?.Id))
        {
            return Error.Conflict("role_name_taken", "api.error.role-name-exists");
        }

        if (await StepUpAsync(request.CurrentPassword, "role", ct) is { } denied)
        {
            return denied;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        string before = "";
        if (role is null)
        {
            role = new ApplicationRole(name) { Title = title };
            var created = await roles.CreateAsync(role);
            if (!created.Succeeded)
            {
                return Error.Conflict("role_name_taken", "api.error.role-name-exists");
            }
        }
        else
        {
            before = string.Join(",", (await roles.GetClaimsAsync(role)).Where(c => c.Type == Permissions.ClaimType).Select(c => c.Value).Order(StringComparer.Ordinal));
            var renamed = role.Name != name;
            role.Name = name;
            role.NormalizedName = roles.NormalizeKey(name);
            role.Title = title;
            await roles.UpdateAsync(role);
            if (renamed)
            {
                await roles.UpdateNormalizedRoleNameAsync(role);
            }
        }

        var existing = (await roles.GetClaimsAsync(role)).Where(c => c.Type == Permissions.ClaimType).ToList();
        foreach (var stale in existing.Where(c => !permissions.Contains(c.Value)))
        {
            await roles.RemoveClaimAsync(role, stale);
        }

        foreach (var added in permissions.Where(p => existing.All(c => c.Value != p)))
        {
            await roles.AddClaimAsync(role, new System.Security.Claims.Claim(Permissions.ClaimType, added));
        }

        // The role's members get the new permission set at their next request.
        foreach (var member in await users.GetUsersInRoleAsync(role.Name!))
        {
            await users.UpdateSecurityStampAsync(member);
        }

        await roles.UpdateAsync(role); // new concurrency stamp
        Audit(before.Length == 0 && id is null ? StaffAuditActions.RoleCreated : StaffAuditActions.RoleUpdated, role.Name,
            id is null ? "permissions=" + string.Join(",", permissions.Order(StringComparer.Ordinal)) : $"{before} -> {string.Join(",", permissions.Order(StringComparer.Ordinal))}");
        await db.SaveChangesAsync(ct);
        if (!await SomeoneKeepsEverythingAsync(ct))
        {
            await tx.RollbackAsync(ct);
            return Lockout;
        }

        await tx.CommitAsync(ct);
        return (await LoadRolesAsync(ct)).Single(r => r.Id == role.Id);
    }

    public async Task<Result> DeleteRoleAsync(Guid id, DeleteRoleRequest request, CancellationToken ct)
    {
        var role = await roles.FindByIdAsync(id.ToString());
        if (role is null)
        {
            return Result.Failure(RoleNotFound);
        }

        if (Roles.All.Contains(role.Name!))
        {
            return Result.Failure(Error.Conflict("role_system", "api.error.role-system"));
        }

        if (role.ConcurrencyStamp != request.Stamp)
        {
            return Result.Failure(Stale);
        }

        if ((await users.GetUsersInRoleAsync(role.Name!)).Count > 0)
        {
            return Result.Failure(Error.Conflict("role_in_use", "api.error.role-in-use"));
        }

        if (await StepUpAsync(request.CurrentPassword, "role", ct) is { } denied)
        {
            return Result.Failure(denied);
        }

        await roles.DeleteAsync(role);
        Audit(StaffAuditActions.RoleDeleted, role.Name, null);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---- audit ---------------------------------------------------------------------------------------------------------

    public async Task<StaffAuditPageDto> GetAuditAsync(int page, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        var total = await db.StaffAuditLogs.CountAsync(ct);
        var items = await (
                from a in db.StaffAuditLogs.AsNoTracking()
                join u in db.Users.AsNoTracking() on a.ActorUserId equals u.Id into actors
                from u in actors.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new StaffAuditEntryDto(a.Id, a.AtUtc, a.ActorUserId, u.UserName, a.Action, a.Subject, a.Detail))
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new StaffAuditPageDto(items, total);
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------------

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : PersianText.Normalize(value.Trim());

    private static bool IsValidRoleName(string name) =>
        name.Length is >= RoleNameMinLength and <= RoleNameMaxLength && char.IsAsciiLetter(name[0])
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private static Error TooLong(string field, int max) =>
        Error.Validation(new Dictionary<string, string[]> { [field] = [Msg.Of("api.validation.text-max-length", ("max", max))] });

    private void Audit(string action, string? subject, string? detail) =>
        db.StaffAuditLogs.Add(StaffAuditLog.Create(currentUser.UserId, action, subject, detail, clock.GetUtcNow().UtcDateTime));

    /// <summary>The user, only if they are staff (a customer's account is not managed here).</summary>
    private async Task<ApplicationUser?> FindStaffAsync(Guid id)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return null;
        }

        return (await users.GetRolesAsync(user)).Any(r => r != Roles.Customer) ? user : null;
    }

    /// <summary>Every name must be an existing role and not Customer (that is not a staff role).</summary>
    private async Task<Error?> CheckRolesAsync(IReadOnlyList<string> names, string field, CancellationToken ct)
    {
        if (names.Count == 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { [field] = ["api.validation.staff-role-required"] });
        }

        var known = await db.Roles.AsNoTracking().Select(r => r.Name!).ToListAsync(ct);
        if (names.Any(n => n == Roles.Customer || !known.Contains(n)))
        {
            return Error.Validation(new Dictionary<string, string[]> { [field] = ["api.validation.staff-role-unknown"] });
        }

        return null;
    }

    /// <summary>
    /// Null when the caller's password is right. A wrong or missing one is a 403, is audited (no values) and counts as a failed
    /// sign-in, so guessing it here leads to the same lockout as the login page.
    /// </summary>
    private async Task<Error?> StepUpAsync(string? password, string what, CancellationToken ct)
    {
        var user = currentUser.UserId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
        var ok = user is not null && !string.IsNullOrEmpty(password) && !await users.IsLockedOutAsync(user)
            && await users.CheckPasswordAsync(user, password);
        if (ok)
        {
            await users.ResetAccessFailedCountAsync(user!);
            return null;
        }

        if (user is not null && !string.IsNullOrEmpty(password))
        {
            await users.AccessFailedAsync(user);
        }

        Audit(StaffAuditActions.ReauthFailed, user?.UserName, what);
        await db.SaveChangesAsync(ct);
        return Error.Forbidden("step_up_failed", "api.error.current-password-required");
    }

    /// <summary>True when some ACTIVE user holds every permission through their roles.</summary>
    private async Task<bool> SomeoneKeepsEverythingAsync(CancellationToken ct)
    {
        var claims = await db.RoleClaims.AsNoTracking().Where(c => c.ClaimType == Permissions.ClaimType)
            .Select(c => new { c.RoleId, c.ClaimValue }).ToListAsync(ct);
        var byRole = claims.GroupBy(c => c.RoleId).ToDictionary(g => g.Key, g => g.Select(c => c.ClaimValue!).ToHashSet(StringComparer.Ordinal));
        var memberships = await (from ur in db.UserRoles.AsNoTracking()
                                 join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                                 where u.IsActive
                                 select new { ur.UserId, ur.RoleId }).ToListAsync(ct);
        return memberships.GroupBy(m => m.UserId).Any(g =>
        {
            var held = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in g)
            {
                if (byRole.TryGetValue(m.RoleId, out var perms))
                {
                    held.UnionWith(perms);
                }
            }

            return Permissions.All.All(held.Contains);
        });
    }
}
