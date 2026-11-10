using Microsoft.EntityFrameworkCore;

// Reads meeting-room data through EF Core.
public sealed class MeetingRoomRepository(MeetingRoomDbContext db) : IMeetingRoomRepository
{
    public async Task<IReadOnlyList<MeetingRoom>> GetAllAsync() =>
        await db.MeetingRooms.AsNoTracking().ToListAsync();

    public async Task<MeetingRoom?> GetByIdAsync(int meetingRoomId) =>
        await db.MeetingRooms.AsNoTracking().SingleOrDefaultAsync(room => room.Id == meetingRoomId);

    public Task<bool> ExistsAsync(int meetingRoomId) =>
        db.MeetingRooms.AnyAsync(room => room.Id == meetingRoomId);
}
