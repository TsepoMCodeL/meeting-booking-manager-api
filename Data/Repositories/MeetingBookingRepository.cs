using Microsoft.EntityFrameworkCore;

// Reads and adds bookings, including checks for overlapping active slots.
public sealed class MeetingBookingRepository(MeetingRoomDbContext db) : IMeetingBookingRepository
{
    public async Task<IReadOnlyList<MeetingBooking>> GetActiveByRoomAsync(int meetingRoomId) =>
        await db.MeetingBookings
            .AsNoTracking()
            .Where(booking => booking.MeetingRoomId == meetingRoomId && booking.MeetingRoomIsBooked)
            .ToListAsync();

    public Task<MeetingBooking?> GetByIdAsync(int bookingId) =>
        db.MeetingBookings.SingleOrDefaultAsync(booking => booking.Id == bookingId);

    public Task<bool> HasConflictAsync(
        int meetingRoomId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int? excludedBookingId = null) =>
        db.MeetingBookings.AnyAsync(booking =>
            booking.MeetingRoomId == meetingRoomId
            && booking.MeetingRoomIsBooked
            && booking.Id != excludedBookingId
            && booking.MeetingStartTime < endTime
            && booking.MeetingEndDate > startTime);

    public void Add(MeetingBooking booking) => db.MeetingBookings.Add(booking);
}
