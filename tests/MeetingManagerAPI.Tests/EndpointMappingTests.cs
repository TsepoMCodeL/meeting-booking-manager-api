using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MeetingManagerAPI.Tests;

// Verifies application outcomes map to the intended HTTP responses.
public class EndpointMappingTests
{
    [Fact]
    public async Task CreateBooking_MapsApplicationConflictToConflict()
    {
        var service = new FakeMeetingRoomService
        {
            CreateResult = OperationResult<MeetingBooking>.Failure(
                new OperationError(OperationErrorCode.BookingConflict, "Meeting room already booked for the selected time slot."))
        };

        var response = await MeetingRoomEndpoints.CreateMeetingRoomBooking(service, CreateBooking());

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
    }

    [Fact]
    public async Task CreateBooking_MapsValidationErrorsToValidationProblemResponse()
    {
        var service = new FakeMeetingRoomService
        {
            CreateResult = OperationResult<MeetingBooking>.Failure(
                new OperationError(
                    OperationErrorCode.Validation,
                    "The booking is invalid.",
                    new Dictionary<string, string[]> { ["MeetingTitle"] = ["Meeting title is required."] }))
        };

        var response = await MeetingRoomEndpoints.CreateMeetingRoomBooking(service, CreateBooking());

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
    }

    [Fact]
    public async Task CreateBooking_MapsSuccessToCreatedWithLocation()
    {
        var booking = CreateBooking();
        booking.Id = 42;
        var service = new FakeMeetingRoomService
        {
            CreateResult = OperationResult<MeetingBooking>.Success(booking)
        };

        var response = await MeetingRoomEndpoints.CreateMeetingRoomBooking(service, booking);

        var created = Assert.IsType<Created<MeetingBooking>>(response);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal("/api/bookings/42", created.Location);
    }

    [Fact]
    public async Task GetRoom_MapsMissingRoomToNotFound()
    {
        var response = await MeetingRoomEndpoints.GetMeetingRoomById(new FakeMeetingRoomService(), 999);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
    }

    private static MeetingBooking CreateBooking()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);
        return new MeetingBooking
        {
            MeetingRoomId = 1,
            MeetingTitle = "Planning",
            MeetingStartTime = start,
            MeetingEndDate = start.AddHours(1)
        };
    }

    // Supplies fixed use-case results so endpoint mapping can be tested in isolation.
    private sealed class FakeMeetingRoomService : IMeetingRoomService
    {
        public OperationResult<MeetingBooking> CreateResult { get; init; } =
            OperationResult<MeetingBooking>.Success(CreateBooking());

        public Task<IReadOnlyList<MeetingRoom>> GetAllMeetingRooms() =>
            Task.FromResult<IReadOnlyList<MeetingRoom>>([]);

        public Task<MeetingRoom?> GetMeetingRoomById(int meetingRoomId) =>
            Task.FromResult<MeetingRoom?>(null);

        public Task<IReadOnlyList<MeetingBooking>> GetBookedTimeSlotsForMeetingRoom(int meetingRoomId) =>
            Task.FromResult<IReadOnlyList<MeetingBooking>>([]);

        public Task<OperationResult<MeetingBooking>> CreateMeetingRoomBooking(MeetingBooking meetingRoom) =>
            Task.FromResult(CreateResult);

        public Task<OperationResult<MeetingBooking>> UpdateMeetingRoomBooking(int bookingId, MeetingBooking meetingRoom) =>
            Task.FromResult(OperationResult<MeetingBooking>.Success(meetingRoom));

        public Task<OperationResult<MeetingBooking>> CancelMeetingRoomBooking(int bookingId) =>
            Task.FromResult(OperationResult<MeetingBooking>.Success(CreateBooking()));
    }
}
