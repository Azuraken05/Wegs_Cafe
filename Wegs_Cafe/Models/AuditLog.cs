using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>Firebase schema: audit_logs/{audit_log_identifier}</summary>
public class AuditLog
{
    [JsonProperty("audit_log_identifier")]
    public string AuditLogIdentifier { get; set; } = string.Empty;

    [JsonProperty("actor_identifier")]
    public string ActorIdentifier { get; set; } = string.Empty;

    [JsonProperty("actor_name")]
    public string ActorName { get; set; } = string.Empty;

    [JsonProperty("actor_role")]
    public string ActorRole { get; set; } = string.Empty;

    [JsonProperty("action_type")]
    public string ActionType { get; set; } = string.Empty;

    [JsonProperty("target_resource")]
    public string TargetResource { get; set; } = string.Empty;

    [JsonProperty("action_description")]
    public string ActionDescription { get; set; } = string.Empty;

    [JsonProperty("recorded_at")]
    public string RecordedAt { get; set; } = IsoTimestamp.Now();

    [JsonIgnore]
    public string RecordedAtLocalDisplay => IsoTimestamp.ToLocal(RecordedAt)?.ToString("MMM d, yyyy h:mm tt") ?? RecordedAt;

    [JsonIgnore]
    public string ActorDisplay => string.IsNullOrWhiteSpace(ActorRole) ? ActorName : $"{ActorName} ({ActorRole})";
}
