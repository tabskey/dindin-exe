namespace Domain.Entities;

public class AuditLog
{
    public long Id { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }

    private AuditLog() { } // EF Core

    public static AuditLog Create(string entityType, string entityId, string action, string payload)
    {
        return new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Payload = payload,
            Timestamp = DateTime.UtcNow
        };
    }
}
