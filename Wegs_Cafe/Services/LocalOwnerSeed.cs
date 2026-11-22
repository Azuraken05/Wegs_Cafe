using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// Default Owner account credentials (Owner / Forones41) seeded locally until live Firebase synchronization is turned on.
/// </summary>
public static class LocalOwnerSeed
{
    public const string OwnerUsername = "Owner";
    public const string OwnerPassword = "Forones41";
    public const string OwnerSecurityPin = "1234";

    public static UserAccount CreateDefaultOwner() => new()
    {
        UserIdentifier = "owner-0001",
        Username = OwnerUsername,
        FullName = "Wegs Cafe Owner",
        EmailAddress = "owner@wegscafe.local",
        RoleName = UserRoles.Owner,
        Permissions = UserPermissions.ForOwner(),
        IsActive = true,
        CreatedBy = "system"
    };
}
