namespace Shop.Application.Common;

/// <summary>The authenticated caller. Identity always comes from the auth cookie, never from request bodies.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
}
