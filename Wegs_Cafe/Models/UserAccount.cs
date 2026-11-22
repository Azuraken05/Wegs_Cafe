using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Firebase schema: users/{user_identifier}</summary>
public class UserAccount
{
    [JsonProperty("user_identifier")]
    public string UserIdentifier { get; set; } = string.Empty;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonProperty("email_address")]
    public string EmailAddress { get; set; } = string.Empty;

    [JsonProperty("role")]
    public string Role { get; set; } = UserRoles.Staff;

    [JsonProperty("security_pin")]
    public string SecurityPin { get; set; } = string.Empty;

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("created_at")]
    public string CreatedAt { get; set; } = IsoTimestamp.Now();

    [JsonProperty("created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsOwner => Role == UserRoles.Owner;

    [JsonIgnore]
    public bool IsOwnerRole => IsOwner;

    [JsonIgnore]
    public bool IsManager => Role == UserRoles.Manager;

    [JsonIgnore]
    public bool IsStaff => Role == UserRoles.Staff;

    [JsonIgnore]
    public bool CanBeDisabled => Role != UserRoles.Owner;

    [JsonIgnore]
    public string RoleDisplayName => string.IsNullOrEmpty(Role)
        ? string.Empty
        : char.ToUpperInvariant(Role[0]) + Role.Substring(1);

    [JsonIgnore]
    public string RoleBadgeText => (Role ?? string.Empty).ToUpperInvariant();

    [JsonIgnore]
    public string StatusBadgeText => IsActive ? "ACTIVE" : "INACTIVE";
}
