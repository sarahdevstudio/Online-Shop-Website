using Shop.Application.Common;

namespace Shop.Application.Staff;

/// <summary>A staff account: a user with at least one role other than Customer. <paramref name="Stamp"/> is the version for 409 checks.</summary>
public sealed record StaffUserDto(
    Guid Id, string Username, string? DisplayName, string? Email, bool IsActive, IReadOnlyList<string> Roles, DateTime CreatedAtUtc, string Stamp);

public sealed record RoleDto(Guid Id, string Name, string? Title, bool IsSystem, IReadOnlyList<string> Permissions, int UserCount, string Stamp);

public sealed record StaffOverviewDto(IReadOnlyList<StaffUserDto> Users, IReadOnlyList<RoleDto> Roles, IReadOnlyList<string> Permissions);

public sealed record CreateStaffRequest(string Username, string? DisplayName, string? Email, string Password, IReadOnlyList<string> Roles, string? CurrentPassword);

public sealed record UpdateStaffRequest(string? DisplayName, string? Email, string Stamp);

/// <summary>Changing who has which role is a permission change: it needs the caller's own password again.</summary>
public sealed record SetStaffRolesRequest(IReadOnlyList<string> Roles, string Stamp, string? CurrentPassword);

public sealed record SetStaffActiveRequest(bool IsActive, string Stamp);

public sealed record ResetStaffPasswordRequest(string NewPassword, string Stamp, string? CurrentPassword);

public sealed record SaveRoleRequest(string Name, string? Title, IReadOnlyList<string> Permissions, string? Stamp, string? CurrentPassword);

public sealed record DeleteRoleRequest(string Stamp, string? CurrentPassword);

public sealed record StaffAuditEntryDto(int Id, DateTime AtUtc, Guid? ActorUserId, string? ActorName, string Action, string? Subject, string? Detail);

public sealed record StaffAuditPageDto(IReadOnlyList<StaffAuditEntryDto> Items, int Total);

public interface IStaffService
{
    Task<StaffOverviewDto> GetOverviewAsync(CancellationToken ct);

    Task<Result<StaffUserDto>> CreateAsync(CreateStaffRequest request, CancellationToken ct);

    Task<Result<StaffUserDto>> UpdateAsync(Guid id, UpdateStaffRequest request, CancellationToken ct);

    Task<Result<StaffUserDto>> SetRolesAsync(Guid id, SetStaffRolesRequest request, CancellationToken ct);

    /// <summary>Deactivating revokes every session at once; reactivating lets the person sign in again with the same password.</summary>
    Task<Result<StaffUserDto>> SetActiveAsync(Guid id, SetStaffActiveRequest request, CancellationToken ct);

    Task<Result<StaffUserDto>> ResetPasswordAsync(Guid id, ResetStaffPasswordRequest request, CancellationToken ct);

    Task<Result<RoleDto>> SaveRoleAsync(Guid? id, SaveRoleRequest request, CancellationToken ct);

    Task<Result> DeleteRoleAsync(Guid id, DeleteRoleRequest request, CancellationToken ct);

    Task<StaffAuditPageDto> GetAuditAsync(int page, CancellationToken ct);
}
