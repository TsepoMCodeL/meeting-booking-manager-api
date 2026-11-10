public interface IMeetingRoomService
{
    Task<IReadOnlyList<MeetingRoom>> GetAllMeetingRooms();

    Task<MeetingRoom?> GetMeetingRoomById(int meetingRoomId);

    Task<IReadOnlyList<MeetingBooking>> GetBookedTimeSlotsForMeetingRoom(int meetingRoomId);

    Task<OperationResult<MeetingBooking>> CreateMeetingRoomBooking(MeetingBooking meetingRoom);

    Task<OperationResult<MeetingBooking>> UpdateMeetingRoomBooking(int meetingRoomId, MeetingBooking meetingRoom);

    Task<OperationResult<MeetingBooking>> CancelMeetingRoomBooking(int meetingRoomId);
}