using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeetingManagerAPI.Tests;

// Verifies known exceptions are returned as safe retryable HTTP responses.
public class ApiExceptionHandlerTests
{
    [Fact]
    public async Task WrappedSqliteBusyException_ReturnsRetryable503()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                DefaultTimeout = 1,
                Pooling = false
            }.ToString();
            await using var lockConnection = new SqliteConnection(connectionString);
            await lockConnection.OpenAsync();
            await using (var setup = lockConnection.CreateCommand())
            {
                setup.CommandText = "CREATE TABLE LockTest (Id INTEGER PRIMARY KEY)";
                await setup.ExecuteNonQueryAsync();
            }

            await using var transaction = await lockConnection.BeginTransactionAsync();
            await using (var insert = lockConnection.CreateCommand())
            {
                insert.CommandText = "INSERT INTO LockTest VALUES (1)";
                await insert.ExecuteNonQueryAsync();
            }

            await using var blockedConnection = new SqliteConnection(connectionString);
            await blockedConnection.OpenAsync();
            SqliteException sqliteException;
            await using (var insert = blockedConnection.CreateCommand())
            {
                insert.CommandText = "INSERT INTO LockTest VALUES (2)";
                sqliteException = await Assert.ThrowsAsync<SqliteException>(() => insert.ExecuteNonQueryAsync());
            }

            var exception = new DbUpdateException("Write failed.", sqliteException);
            var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            var handler = new ApiExceptionHandler(
                services.GetRequiredService<IProblemDetailsService>(),
                services.GetRequiredService<ILogger<ApiExceptionHandler>>());
            var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

            Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            using var response = await System.Text.Json.JsonDocument.ParseAsync(context.Response.Body);
            Assert.Equal("Database temporarily unavailable", response.RootElement.GetProperty("title").GetString());
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }
}
