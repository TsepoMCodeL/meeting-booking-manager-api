using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MeetingManagerAPI.Tests;

// Verifies EF migrations create the expected fresh SQLite schema.
public class MigrationTests
{
    [Fact]
    public async Task Migrate_CreatesDatabaseSchema()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<MeetingRoomDbContext>()
                .UseSqlite($"Data Source={databasePath};Pooling=False")
                .Options;
            await using var db = new MeetingRoomDbContext(options);
            await db.Database.MigrateAsync();

            Assert.Empty(await db.MeetingBookings.ToListAsync());
            Assert.Empty(await db.MeetingRooms.ToListAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }
}
