using Microsoft.Extensions.Logging;

namespace Harmonee.SharedKernel.AspNetCore;

/// <summary>
/// Records security-relevant actions as structured events in the <c>Harmonee.Audit</c> log category,
/// which flows to Application Insights / Log Analytics via OpenTelemetry.
/// </summary>
public sealed partial class AuditLog(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("Harmonee.Audit");

    public void AdminAccess(string adminObjectId, Guid familyId, string method, string path) =>
        LogAdminAccess(_logger, adminObjectId, familyId, method, path);

    public void FamilyChanged(Guid actorUserId, Guid familyId, string action, string? target = null) =>
        LogFamilyChanged(_logger, actorUserId, familyId, action, target);

    [LoggerMessage(EventId = 9000, Level = LogLevel.Information,
        Message = "Platform admin {AdminObjectId} accessed family {FamilyId} via admin override: {HttpMethod} {Path}")]
    private static partial void LogAdminAccess(ILogger logger, string adminObjectId, Guid familyId, string httpMethod, string path);

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information,
        Message = "User {ActorUserId} performed {AuditAction} on family {FamilyId} (target {AuditTarget})")]
    private static partial void LogFamilyChanged(ILogger logger, Guid actorUserId, Guid familyId, string auditAction, string? auditTarget);
}
