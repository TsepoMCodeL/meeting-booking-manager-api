using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// Creates the SQLite context used by EF migration tooling.
public sealed class MeetingRoomDbContextFactory : IDesignTimeDbContextFactory<MeetingRoomDbContext>
{
    public MeetingRoomDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MeetingRoomDbContext>()
            .UseSqlite("Data Source=meetingrooms.db")
            .Options;

        return new MeetingRoomDbContext(options);
    }
}
