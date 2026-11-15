using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>users/{uid}</summary>
public class AppUser
{
    [JsonProperty("uid")] public string Uid { get; set; } = string.Empty;
    /// <summary>Login name (extra field; Firebase will map it to a synthetic email later).</summary>
    [JsonProperty("username")] public string Username { get; set; } = string.Empty;
    [JsonProperty("full_name")] public string FullName { get; set; } = string.Empty;
    [JsonProperty("email")] public string Email { get; set; } = string.Empty;
    [JsonProperty("role")] public string Role { get; set; } = Roles.Staff;
    /// <summary>Salted hash of the 4-6 digit PIN. Never the raw PIN.</summary>
    [JsonProperty("pin_code")] public string PinCode { get; set; } = string.Empty;
    [JsonProperty("is_active")] public bool IsActive { get; set; } = true;
    [JsonProperty("created_at")] public string CreatedAt { get; set; } = IsoDate.Now();
    [JsonProperty("created_by")] public string CreatedBy { get; set; } = string.Empty;

    [JsonIgnore] public bool IsOwner => Role == Roles.Owner;
    [JsonIgnore] public bool IsManager => Role == Roles.Manager;
    [JsonIgnore] public bool CanBeDisabled => Role != Roles.Owner;
    [JsonIgnore] public string RoleDisplay => string.IsNullOrEmpty(Role) ? "" : char.ToUpper(Role[0]) + Role.Substring(1);
}
