public interface IMeetingBookingRepository
{
    Task<IReadOnlyList<MeetingBooking>> GetActiveByRoomAsync(int meetingRoomId);

    Task<MeetingBooking?> GetByIdAsync(int bookingId);

    Task<bool> HasConflictAsync(
        int meetingRoomId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int? excludedBookingId = null);

    void Add(MeetingBooking booking);
}
