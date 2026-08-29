namespace Domain.Entities;

public class IdempotencyRecord
{
    public string Key { get; private set; } = string.Empty;
    public string RequestPath { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public int ResponseStatusCode { get; private set; }
    public string ResponseBody { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private IdempotencyRecord() { } // EF Core

    public static IdempotencyRecord Create(string key, string requestPath, string requestHash, int statusCode, string responseBody)
    {
        return new IdempotencyRecord
        {
            Key = key,
            RequestPath = requestPath,
            RequestHash = requestHash,
            ResponseStatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAt = DateTime.UtcNow
        };
    }
}
