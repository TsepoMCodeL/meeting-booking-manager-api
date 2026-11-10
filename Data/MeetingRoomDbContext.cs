using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

// Defines EF entities and stores booking timestamps as UTC text in SQLite.
public class MeetingRoomDbContext(DbContextOptions<MeetingRoomDbContext> options) : DbContext(options)
{
    public DbSet<MeetingRoom> MeetingRooms => Set<MeetingRoom>();
    public DbSet<MeetingBooking> MeetingBookings => Set<MeetingBooking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var utcTextConverter = new ValueConverter<DateTimeOffset, string>(
            value => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'"),
            value => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));

        modelBuilder.Entity<MeetingBooking>()
            .ToTable("MeetingManagers");
        modelBuilder.Entity<MeetingBooking>()
            .Property(booking => booking.MeetingStartTime)
            .HasConversion(utcTextConverter);
        modelBuilder.Entity<MeetingBooking>()
            .Property(booking => booking.MeetingEndDate)
            .HasConversion(utcTextConverter);
    }
}