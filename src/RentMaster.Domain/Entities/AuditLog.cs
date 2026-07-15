namespace RentMaster.Domain.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? UserId { get; set; }
    public required string HttpMethod { get; set; }
    public required string Path { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public required string TraceId { get; set; }
}
