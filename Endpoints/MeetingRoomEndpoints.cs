// Defines authenticated room and booking routes and maps application results to HTTP.
public static class MeetingRoomEndpoints
{
    public static void MapMeetingRoomEndpoints(this WebApplication app)
    {
        var rooms = app.MapGroup("/api/rooms").RequireAuthorization();
        var bookings = app.MapGroup("/api/bookings").RequireAuthorization();

        rooms.MapGet("/", GetAllMeetingRooms);
        rooms.MapGet("/{meetingRoomId}", GetMeetingRoomById);
        bookings.MapGet("/{meetingRoomId}", GetBookedTimeSlotsForMeetingRoom);
        bookings.MapPost("/", CreateMeetingRoomBooking);
        bookings.MapPut("/{bookingId}", UpdateMeetingRoomBooking);
        bookings.MapDelete("/{bookingId}", CancelMeetingRoomBooking);
    }

    public static async Task<IResult> GetAllMeetingRooms(IMeetingRoomService meetingRoomService) =>
        TypedResults.Ok(await meetingRoomService.GetAllMeetingRooms());

    public static async Task<IResult> GetMeetingRoomById(IMeetingRoomService meetingRoomService, int meetingRoomId) =>
        await meetingRoomService.GetMeetingRoomById(meetingRoomId) is { } meetingRoom
            ? TypedResults.Ok(meetingRoom)
            : TypedResults.NotFound();

    public static async Task<IResult> GetBookedTimeSlotsForMeetingRoom(IMeetingRoomService meetingRoomService, int meetingRoomId) =>
        TypedResults.Ok(await meetingRoomService.GetBookedTimeSlotsForMeetingRoom(meetingRoomId));

    public static async Task<IResult> CreateMeetingRoomBooking(IMeetingRoomService meetingRoomService, MeetingBooking meetingBooking)
    {
        var result = await meetingRoomService.CreateMeetingRoomBooking(meetingBooking);
        return result.Succeeded
            ? TypedResults.Created($"/api/bookings/{result.Value!.Id}", result.Value)
            : MapError(result.Error!);
    }

    public static async Task<IResult> UpdateMeetingRoomBooking(IMeetingRoomService meetingRoomService, int bookingId, MeetingBooking meetingBooking)
    {
        var result = await meetingRoomService.UpdateMeetingRoomBooking(bookingId, meetingBooking);
        return result.Succeeded
            ? TypedResults.Ok(new { message = "Booking updated successfully." })
            : MapError(result.Error!);
    }

    public static async Task<IResult> CancelMeetingRoomBooking(IMeetingRoomService meetingRoomService, int bookingId)
    {
        var result = await meetingRoomService.CancelMeetingRoomBooking(bookingId);
        return result.Succeeded
            ? TypedResults.Ok(new { message = "Meeting room booking cancelled successfully." })
            : MapError(result.Error!);
    }

    private static IResult MapError(OperationError error)
    {
        return error.Code switch
        {
            OperationErrorCode.Validation => TypedResults.ValidationProblem(error.ValidationErrors!),
            OperationErrorCode.RoomNotFound or OperationErrorCode.BookingNotFound =>
                TypedResults.NotFound(new { message = error.Message }),
            OperationErrorCode.BookingConflict => TypedResults.Conflict(new { message = error.Message }),
            _ => TypedResults.BadRequest(new { message = error.Message })
        };
    }
}
