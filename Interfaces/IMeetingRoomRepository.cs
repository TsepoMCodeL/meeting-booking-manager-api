public interface IMeetingRoomRepository
{
    Task<IReadOnlyList<MeetingRoom>> GetAllAsync();

    Task<MeetingRoom?> GetByIdAsync(int meetingRoomId);

    Task<bool> ExistsAsync(int meetingRoomId);
}
