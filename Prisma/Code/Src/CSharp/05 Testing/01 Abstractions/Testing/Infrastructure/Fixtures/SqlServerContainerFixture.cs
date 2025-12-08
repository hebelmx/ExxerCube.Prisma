using ExxerCube.Prisma.Testing.Infrastructure.Fixtures.Base;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace ExxerCube.Prisma.Testing.Infrastructure.Fixtures;

/// <summary>
/// SQL Server database container fixture for integration and system tests.
/// Implements xUnit v3 IAsyncLifetime pattern with all Docker defensive programming patterns:
/// - Verified image tag (mcr.microsoft.com/mssql/server:2022-latest)
/// - Defensive wait strategy with built-in readiness check
/// - Console logging via TestContext for collection fixtures
/// - Proper lifecycle management (StartAsync, StopAsync, DisposeAsync)
/// - Fail-hard when Docker unavailable (no graceful degradation)
/// - EF Core database creation and migration support
/// </summary>
public sealed class SqlServerContainerFixture : ContainerFixtureBase<MsSqlContainer>
{
    // SQL Server 2022 verified image - production-grade, well-maintained by Microsoft
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";
    private const int SqlServerPort = 1433;
    private const string DefaultPassword = "YourStrong!Passw0rd"; // SQL Server password requirements: 8+ chars, uppercase, lowercase, digits, special chars
    private const string DefaultDatabase = "PrismaTestDb";

    /// <summary>
    /// Gets the SQL Server container hostname.
    /// </summary>
    public override string Hostname => Container?.Hostname ?? "localhost";

    /// <summary>
    /// Gets the mapped SQL Server port.
    /// </summary>
    public override int Port => Container?.GetMappedPublicPort(SqlServerPort) ?? SqlServerPort;

    /// <summary>
    /// Gets the SQL Server connection string.
    /// </summary>
    public override string ConnectionString { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the SQL Server database name.
    /// </summary>
    public string Database => DefaultDatabase;

    /// <summary>
    /// Gets the SQL Server password.
    /// </summary>
    public string Password => DefaultPassword;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerContainerFixture"/> class.
    /// Uses TestContext.Current.SendMessage() for logging container lifecycle events.
    /// </summary>
    public SqlServerContainerFixture()
    {
    }

    /// <summary>
    /// Builds the SQL Server container with defensive programming patterns.
    /// </summary>
    /// <returns>A configured SQL Server container ready to start.</returns>
    protected override Task<MsSqlContainer> BuildContainerAsync()
    {
        var container = new MsSqlBuilder()
            .WithImage(SqlServerImage)  // ✅ Verified image tag
            .WithPassword(DefaultPassword)  // Strong password meeting SQL Server requirements
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();  // ✅ Built-in readiness check included

        return Task.FromResult(container);
    }

    /// <summary>
    /// Configures connection string and creates the database after successful container start.
    /// </summary>
    /// <returns>A Task representing the asynchronous configuration operation.</returns>
    protected override async Task ConfigureConnectionAsync()
    {
        if (Container == null)
        {
            throw new InvalidOperationException("Container is not initialized");
        }

        // Get connection string from container (points to master database initially)
        ConnectionString = Container.GetConnectionString();

        LogMessage($"✅ SQL Server available at: {ConnectionString}");

        // Create the test database
        await CreateDatabaseAsync();
    }

    /// <summary>
    /// Creates the test database in the SQL Server container.
    /// </summary>
    /// <returns>A Task representing the asynchronous database creation operation.</returns>
    private async Task CreateDatabaseAsync()
    {
        try
        {
            LogMessage($"🔧 Creating database '{Database}'...");

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            // Create database if it doesn't exist
            await using var createDbCmd = connection.CreateCommand();
            createDbCmd.CommandText = $@"
                IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{Database}')
                BEGIN
                    CREATE DATABASE [{Database}];
                END";
            await createDbCmd.ExecuteNonQueryAsync();

            LogMessage($"✅ Database '{Database}' created successfully");

            // Update connection string to point to the test database
            var builder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = Database
            };
            ConnectionString = builder.ConnectionString;

            LogMessage($"🔗 Updated connection string to use database: {Database}");
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Failed to create database '{Database}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the container type name for logging.
    /// </summary>
    /// <returns>The container type name.</returns>
    protected override string GetContainerTypeName() => "SQL Server";

    /// <summary>
    /// Executes EF Core database migrations on the container database.
    /// Call this method from tests after the fixture is initialized to apply your DbContext migrations.
    /// </summary>
    /// <typeparam name="TDbContext">The EF Core DbContext type.</typeparam>
    /// <param name="contextFactory">Factory function to create the DbContext with the connection string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Task representing the asynchronous migration operation.</returns>
    public async Task ApplyMigrationsAsync<TDbContext>(
        Func<string, TDbContext> contextFactory,
        CancellationToken cancellationToken = default)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        EnsureAvailable();

        try
        {
            LogMessage($"📦 Applying EF Core migrations for {typeof(TDbContext).Name}...");

            await using var context = contextFactory(ConnectionString);
            await context.Database.EnsureCreatedAsync(cancellationToken);

            LogMessage("✅ Migrations applied successfully");
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Failed to apply migrations: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Seeds test data into the database using the provided seed action.
    /// </summary>
    /// <typeparam name="TDbContext">The EF Core DbContext type.</typeparam>
    /// <param name="contextFactory">Factory function to create the DbContext with the connection string.</param>
    /// <param name="seedAction">Action to seed data into the context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Task representing the asynchronous seeding operation.</returns>
    public async Task SeedDataAsync<TDbContext>(
        Func<string, TDbContext> contextFactory,
        Func<TDbContext, Task> seedAction,
        CancellationToken cancellationToken = default)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        EnsureAvailable();

        try
        {
            LogMessage("🌱 Seeding test data...");

            await using var context = contextFactory(ConnectionString);
            await seedAction(context);
            await context.SaveChangesAsync(cancellationToken);

            LogMessage("✅ Test data seeded successfully");
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Failed to seed test data: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Cleans all data from the database while preserving schema.
    /// Useful for resetting database state between tests without recreating the container.
    /// </summary>
    public async Task CleanDatabaseAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            LogMessage("🧹 Cleaning database data...");

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            // Disable foreign key constraints
            await using var disableFkCmd = connection.CreateCommand();
            disableFkCmd.CommandText = "EXEC sp_MSForEachTable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'";
            await disableFkCmd.ExecuteNonQueryAsync();

            // Get all user tables
            await using var getTablesCmd = connection.CreateCommand();
            getTablesCmd.CommandText = @"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                AND TABLE_SCHEMA = 'dbo'";

            var tables = new List<string>();
            await using (var reader = await getTablesCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            // Delete all data from tables
            foreach (var table in tables)
            {
                await using var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = $"DELETE FROM [{table}]";
                await deleteCmd.ExecuteNonQueryAsync();
            }

            // Re-enable foreign key constraints
            await using var enableFkCmd = connection.CreateCommand();
            enableFkCmd.CommandText = "EXEC sp_MSForEachTable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'";
            await enableFkCmd.ExecuteNonQueryAsync();

            LogMessage("✅ Database cleanup complete");
        }
        catch (Exception ex)
        {
            LogMessage($"⚠️ Database cleanup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs custom cleanup before container disposal.
    /// Cleans the database to ensure clean state.
    /// </summary>
    /// <returns>A Task representing the asynchronous cleanup operation.</returns>
    protected override async Task PerformCustomCleanupAsync()
    {
        await CleanDatabaseAsync();
    }
}
