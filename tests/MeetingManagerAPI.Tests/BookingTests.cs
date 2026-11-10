using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MeetingManagerAPI.Tests;

// Verifies booking validation, conflicts, persistence, updates, and cancellation.
public class BookingTests
{
    [Fact]
    public async Task CreateBooking_MissingRoom_ReturnsNotFound()
    {
        await using var db = CreateDatabase();
        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(CreateBooking(roomId: 99));

        Assert.Equal(OperationErrorCode.RoomNotFound, result.Error!.Code);
        Assert.Empty(await db.MeetingBookings.ToListAsync());
    }

    [Fact]
    public async Task GetMeetingRoomById_ReturnsRoom()
    {
        await using var db = CreateDatabase();
        var room = new MeetingRoom { Id = 1, Name = "Indigo", Capacity = 6 };
        db.MeetingRooms.Add(room);
        await db.SaveChangesAsync();

        var result = await CreateMeetingRoomService(db).GetMeetingRoomById(room.Id);

        Assert.Equal(room.Id, result!.Id);
    }

    [Fact]
    public async Task CreateBooking_ValidRequest_MarksBookingActiveAndReturnsItsLocation()
    {
        await using var db = CreateDatabase();
        await db.MeetingRooms.AddAsync(new MeetingRoom { Id = 1, Name = "Indigo", Capacity = 6 });
        await db.SaveChangesAsync();
        var booking = CreateBooking();

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(booking);

        Assert.True(result.Succeeded);
        Assert.True(booking.MeetingRoomIsBooked);
        Assert.Equal(booking.Id, result.Value!.Id);
    }

    [Fact]
    public async Task CreateBooking_OffsetTime_IsStoredInUtcAndConflictsWithUtcInput()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        var booking = CreateBooking();
        booking.MeetingStartTime = booking.MeetingStartTime.ToOffset(TimeSpan.FromHours(2));
        booking.MeetingEndDate = booking.MeetingEndDate.ToOffset(TimeSpan.FromHours(2));

        var created = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(booking);
        var overlapping = CreateBooking();
        overlapping.MeetingStartTime = booking.MeetingStartTime.ToUniversalTime();
        overlapping.MeetingEndDate = booking.MeetingEndDate.ToUniversalTime();
        var conflict = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(overlapping);

        Assert.True(created.Succeeded);
        Assert.Equal(TimeSpan.Zero, booking.MeetingStartTime.Offset);
        Assert.Equal(OperationErrorCode.BookingConflict, conflict.Error!.Code);
    }

    [Fact]
    public async Task CreateBooking_OverlappingActiveBooking_ReturnsBadRequest()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        var existing = CreateBooking();
        existing.MeetingRoomIsBooked = true;
        db.MeetingBookings.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(CreateBooking());

        Assert.Equal(OperationErrorCode.BookingConflict, result.Error!.Code);
        Assert.Single(await db.MeetingBookings.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentOverlappingBookings_OnlyOneSucceeds()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<MeetingRoomDbContext>()
            .UseSqlite($"Data Source={databasePath};Default Timeout=30;Pooling=False")
            .Options;

        try
        {
            await using (var setup = new MeetingRoomDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                await setup.MeetingRooms.AddAsync(new MeetingRoom { Id = 1, Name = "Indigo", Capacity = 6 });
                await setup.SaveChangesAsync();
            }

            await using var firstDb = new MeetingRoomDbContext(options);
            await using var secondDb = new MeetingRoomDbContext(options);
            var first = CreateMeetingRoomService(firstDb).CreateMeetingRoomBooking(CreateBooking());
            var second = CreateMeetingRoomService(secondDb).CreateMeetingRoomBooking(CreateBooking());

            var results = await Task.WhenAll(first, second);

            Assert.Single(results, result => result.Succeeded);
            Assert.Single(results, result => result.Error?.Code == OperationErrorCode.BookingConflict);
            Assert.Single(await firstDb.MeetingBookings.ToListAsync());
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CreateBooking_AdjacentTimeSlot_IsAllowed()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        var existing = CreateBooking();
        existing.MeetingRoomIsBooked = true;
        db.MeetingBookings.Add(existing);
        await db.SaveChangesAsync();
        var adjacent = CreateBooking();
        adjacent.MeetingStartTime = existing.MeetingEndDate;
        adjacent.MeetingEndDate = adjacent.MeetingStartTime.AddHours(1);

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(adjacent);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CreateBooking_OverlappingCancelledBooking_IsAllowed()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        db.MeetingBookings.Add(CreateBooking());
        await db.SaveChangesAsync();

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(CreateBooking());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UpdateBooking_DoesNotConflictWithItself_AndChangesRoom()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        await db.MeetingRooms.AddAsync(new MeetingRoom { Id = 2, Name = "Johto", Capacity = 10 });
        var existing = CreateBooking();
        existing.MeetingRoomIsBooked = true;
        db.MeetingBookings.Add(existing);
        await db.SaveChangesAsync();
        var updated = CreateBooking(roomId: 2);
        updated.MeetingTitle = "Updated";

        var result = await CreateMeetingRoomService(db).UpdateMeetingRoomBooking(existing.Id, updated);

        Assert.True(result.Succeeded);
        Assert.Equal(2, existing.MeetingRoomId);
        Assert.Equal("Updated", existing.MeetingTitle);
    }

    [Fact]
    public async Task UpdateBooking_OverlappingAnotherActiveBooking_ReturnsBadRequest()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        var existing = CreateBooking();
        existing.MeetingRoomIsBooked = true;
        var other = CreateBooking();
        other.MeetingRoomIsBooked = true;
        other.MeetingStartTime = existing.MeetingStartTime.AddMinutes(30);
        other.MeetingEndDate = existing.MeetingEndDate.AddMinutes(30);
        db.MeetingBookings.AddRange(existing, other);
        await db.SaveChangesAsync();

        var result = await CreateMeetingRoomService(db).UpdateMeetingRoomBooking(existing.Id, CreateBooking());

        Assert.Equal(OperationErrorCode.BookingConflict, result.Error!.Code);
    }

    [Fact]
    public async Task CreateBooking_EndBeforeStart_ReturnsBadRequest()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        var booking = CreateBooking();
        booking.MeetingEndDate = booking.MeetingStartTime;

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(booking);

        Assert.Equal(OperationErrorCode.Validation, result.Error!.Code);
        Assert.Empty(await db.MeetingBookings.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateBooking_EmptyTitle_ReturnsValidationProblem(string title)
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        var booking = CreateBooking();
        booking.MeetingTitle = title;

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(booking);

        Assert.Equal(OperationErrorCode.Validation, result.Error!.Code);
        Assert.Contains("MeetingTitle", result.Error.ValidationErrors!.Keys);
        Assert.Empty(await db.MeetingBookings.ToListAsync());
    }

    [Fact]
    public async Task CreateBooking_InvalidRoomId_ReturnsValidationProblem()
    {
        await using var db = CreateDatabase();
        var booking = CreateBooking(roomId: 0);

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(booking);

        Assert.Equal(OperationErrorCode.Validation, result.Error!.Code);
        Assert.Empty(await db.MeetingBookings.ToListAsync());
    }

    [Fact]
    public async Task CreateBooking_TitleOverMaximumLength_ReturnsValidationProblem()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        var booking = CreateBooking();
        booking.MeetingTitle = new string('a', 201);

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(booking);

        Assert.Equal(OperationErrorCode.Validation, result.Error!.Code);
        Assert.Empty(await db.MeetingBookings.ToListAsync());
    }

    [Fact]
    public async Task CreateBooking_StartInPast_ReturnsValidationProblem()
    {
        await using var db = CreateDatabase();
        await SeedRoom(db);
        var booking = CreateBooking();
        booking.MeetingStartTime = DateTime.UtcNow.AddMinutes(-1);
        booking.MeetingEndDate = DateTime.UtcNow.AddMinutes(30);

        var result = await CreateMeetingRoomService(db).CreateMeetingRoomBooking(booking);

        Assert.Equal(OperationErrorCode.Validation, result.Error!.Code);
        Assert.Empty(await db.MeetingBookings.ToListAsync());
    }

    [Fact]
    public void BookingJson_RequiresExplicitTimezoneOffset()
    {
        const string withoutOffset = """{"meetingRoomId":1,"meetingTitle":"Planning","meetingStartTime":"2026-11-11T09:00:00","meetingEndDate":"2026-11-11T10:00:00"}""";
        const string withOffset = """{"meetingRoomId":1,"meetingTitle":"Planning","meetingStartTime":"2026-11-11T09:00:00+02:00","meetingEndDate":"2026-11-11T10:00:00+02:00"}""";

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MeetingBooking>(withoutOffset, options));
        var booking = JsonSerializer.Deserialize<MeetingBooking>(withOffset, options)!;
        Assert.Equal(TimeSpan.Zero, booking.MeetingStartTime.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 7, 0, 0, TimeSpan.Zero), booking.MeetingStartTime);
    }

    [Fact]
    public async Task CancelBooking_StartedMeeting_ReturnsBadRequest()
    {
        await using var db = CreateDatabase();
        var booking = CreateBooking();
        booking.MeetingStartTime = DateTime.UtcNow.AddMinutes(-1);
        booking.MeetingEndDate = DateTime.UtcNow.AddMinutes(30);
        booking.MeetingRoomIsBooked = true;
        db.MeetingBookings.Add(booking);
        await db.SaveChangesAsync();

        var result = await CreateMeetingRoomService(db).CancelMeetingRoomBooking(booking.Id);

        Assert.Equal(OperationErrorCode.MeetingAlreadyStarted, result.Error!.Code);
        Assert.True(booking.MeetingRoomIsBooked);
    }

    [Fact]
    public async Task CancelBooking_FutureMeeting_MarksBookingInactive()
    {
        await using var db = CreateDatabase();
        var booking = CreateBooking();
        booking.MeetingRoomIsBooked = true;
        db.MeetingBookings.Add(booking);
        await db.SaveChangesAsync();

        var result = await CreateMeetingRoomService(db).CancelMeetingRoomBooking(booking.Id);

        Assert.True(result.Succeeded);
        Assert.False(booking.MeetingRoomIsBooked);
    }

    private static MeetingRoomDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<MeetingRoomDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new MeetingRoomDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task SeedRoom(MeetingRoomDbContext db)
    {
        await db.MeetingRooms.AddAsync(new MeetingRoom { Id = 1, Name = "Indigo", Capacity = 6 });
        await db.SaveChangesAsync();
    }

    private static MeetingRoomService CreateMeetingRoomService(MeetingRoomDbContext db) =>
        new(new MeetingRoomRepository(db), new MeetingBookingRepository(db), new EfUnitOfWork(db));

    private static MeetingBooking CreateBooking(int roomId = 1)
    {
        var start = DateTime.UtcNow.AddDays(1);
        return new MeetingBooking
        {
            MeetingRoomId = roomId,
            MeetingTitle = "Planning",
            MeetingStartTime = start,
            MeetingEndDate = start.AddHours(1)
        };
    }
}
