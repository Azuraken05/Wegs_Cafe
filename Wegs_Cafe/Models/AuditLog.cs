using Newtonsoft.Json;

namespace Wegs_Cafe.Models;

/// <summary>audit_logs/{audit_id}. Append-only: the app never edits or deletes these.</summary>
public class AuditLog
{
    [JsonProperty("audit_id")] public string AuditId { get; set; } = string.Empty;
    [JsonProperty("actor_uid")] public string ActorUid { get; set; } = string.Empty;
    [JsonProperty("actor_name")] public string ActorName { get; set; } = string.Empty;
    [JsonProperty("actor_role")] public string ActorRole { get; set; } = string.Empty;
    [JsonProperty("action")] public string Action { get; set; } = string.Empty;
    [JsonProperty("target_entity")] public string TargetEntity { get; set; } = string.Empty;
    [JsonProperty("details")] public string Details { get; set; } = string.Empty;
    [JsonProperty("timestamp")] public string Timestamp { get; set; } = IsoDate.Now();

    [JsonIgnore] public string TimestampDisplay => IsoDate.ToLocal(Timestamp)?.ToString("MMM d, yyyy h:mm tt") ?? Timestamp;
    [JsonIgnore] public string ActorDisplay => $"{ActorName} ({ActorRole})";
}
