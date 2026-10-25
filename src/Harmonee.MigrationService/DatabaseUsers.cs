using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Harmonee.MigrationService;

/// <summary>An Entra workload identity (for example the API's managed identity) that needs a user in the database.</summary>
internal sealed class DatabaseUser
{
    public string Name { get; set; } = "";

    /// <summary>The identity's client (application) ID, which is the SID of its database user.</summary>
    public Guid ClientId { get; set; }

    public IList<string> Roles { get; } = [];
}

internal sealed class DatabaseUsersOptions
{
    public const string SectionName = "Database";

    public IList<DatabaseUser> Users { get; } = [];
}

/// <summary>
/// Creates database users for the application's managed identities and grants them fixed database roles. In Azure the
/// migration identity is the server's Entra administrator, so this replaces a manual post-deployment SQL step. Users
/// are created from the client ID (<c>WITH SID ... TYPE = E</c>), so the server needs no Microsoft Graph permissions.
/// </summary>
internal sealed partial class DatabaseUserProvisioner(
    IConfiguration configuration,
    IOptions<DatabaseUsersOptions> options,
    ILogger<DatabaseUserProvisioner> logger)
{
    /// <summary>Only fixed roles an application identity should ever need. db_owner is deliberately absent.</summary>
    private static readonly HashSet<string> s_allowedRoles = new(StringComparer.OrdinalIgnoreCase) { "db_datareader", "db_datawriter", "db_ddladmin" };

    // Recreates the user if its identity was replaced (same name, new client ID), so role grants never go to a stale SID.
    private const string EnsureUserSql = """
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;
        DECLARE @sid VARBINARY(16) = CONVERT(VARBINARY(16), @clientId);
        DECLARE @existingSid VARBINARY(85) = (SELECT sid FROM sys.database_principals WHERE name = @name AND type = 'E');
        IF @existingSid IS NOT NULL AND @existingSid <> @sid
        BEGIN
            DECLARE @drop NVARCHAR(MAX) = N'DROP USER ' + QUOTENAME(@name);
            EXEC (@drop);
            SET @existingSid = NULL;
        END
        IF @existingSid IS NULL
        BEGIN
            DECLARE @create NVARCHAR(MAX) = N'CREATE USER ' + QUOTENAME(@name) + N' WITH SID = ' + CONVERT(VARCHAR(64), @sid, 1) + N', TYPE = E';
            EXEC (@create);
        END
        COMMIT TRANSACTION;
        """;

    private const string AddRoleMemberSql = """
        DECLARE @alter NVARCHAR(MAX) = N'ALTER ROLE ' + QUOTENAME(@role) + N' ADD MEMBER ' + QUOTENAME(@name);
        EXEC (@alter);
        """;

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        var users = options.Value.Users;
        if (users.Count == 0)
        {
            return;
        }

        foreach (var user in users)
        {
            Validate(user);
        }

        await using var connection = new SqlConnection(configuration.GetConnectionString(Harmonee.SharedKernel.ResourceNames.Database));
        connection.RetryLogicProvider = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(new SqlRetryLogicOption
        {
            NumberOfTries = 6,
            DeltaTime = TimeSpan.FromSeconds(2),
            MaxTimeInterval = TimeSpan.FromSeconds(30),
        });
        await connection.OpenAsync(cancellationToken);

        foreach (var user in users)
        {
            LogEnsuringUser(logger, user.Name, user.Roles);

            await using (var command = new SqlCommand(EnsureUserSql, connection))
            {
                command.Parameters.Add(new SqlParameter("@name", System.Data.SqlDbType.NVarChar, 128) { Value = user.Name });
                command.Parameters.Add(new SqlParameter("@clientId", System.Data.SqlDbType.UniqueIdentifier) { Value = user.ClientId });
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var role in user.Roles)
            {
                await using var command = new SqlCommand(AddRoleMemberSql, connection);
                command.Parameters.Add(new SqlParameter("@role", System.Data.SqlDbType.NVarChar, 128) { Value = role });
                command.Parameters.Add(new SqlParameter("@name", System.Data.SqlDbType.NVarChar, 128) { Value = user.Name });
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    internal static void Validate(DatabaseUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(user.Name) || user.Name.Length > 128)
        {
            throw new InvalidOperationException("Database:Users entries need a Name of 1-128 characters.");
        }

        if (user.ClientId == Guid.Empty)
        {
            throw new InvalidOperationException($"Database user '{user.Name}' needs a ClientId.");
        }

        if (user.Roles.FirstOrDefault(role => !s_allowedRoles.Contains(role)) is { } invalid)
        {
            throw new InvalidOperationException(
                $"Database user '{user.Name}' asks for role '{invalid}'. Allowed: {string.Join(", ", s_allowedRoles)}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ensuring database user {UserName} with roles {Roles}")]
    private static partial void LogEnsuringUser(ILogger logger, string userName, IEnumerable<string> roles);
}
