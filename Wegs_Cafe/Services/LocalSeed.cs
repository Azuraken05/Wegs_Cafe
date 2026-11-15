using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

/// <summary>
/// TEMPORARY local owner account until Firebase Auth is integrated.
/// Remove this file (and LocalUserStore) when switching to Firebase.
/// </summary>
public static class LocalSeed
{
    public const string OwnerUsername = "Owner";
    public const string OwnerPassword = "Forones41";
    /// <summary>Default owner PIN for void authorization. TEMPORARY - change after Firebase integration.</summary>
    public const string OwnerPin = "1234";

    public static AppUser CreateOwner() => new()
    {
        Uid = "local-owner",
        Username = OwnerUsername,
        FullName = "Owner",
        Email = "owner@wegs.local",
        Role = Roles.Owner,
        IsActive = true,
        CreatedBy = "system"
    };
}

