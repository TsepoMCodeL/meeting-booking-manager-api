using System.ComponentModel.DataAnnotations;

// Validates and executes meeting-room and booking use cases.
public class MeetingRoomService : IMeetingRoomService
{
    private readonly IMeetingRoomRepository _meetingRooms;
    private readonly IMeetingBookingRepository _bookings;
    private readonly IUnitOfWork _unitOfWork;

    public MeetingRoomService(
        IMeetingRoomRepository meetingRooms,
        IMeetingBookingRepository bookings,
        IUnitOfWork unitOfWork)
    {
        _meetingRooms = meetingRooms;
        _bookings = bookings;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<MeetingRoom>> GetAllMeetingRooms() => _meetingRooms.GetAllAsync();

    public Task<IReadOnlyList<MeetingBooking>> GetBookedTimeSlotsForMeetingRoom(int meetingRoomId) =>
        _bookings.GetActiveByRoomAsync(meetingRoomId);

    public Task<MeetingRoom?> GetMeetingRoomById(int meetingRoomId) => _meetingRooms.GetByIdAsync(meetingRoomId);

    public async Task<OperationResult<MeetingBooking>> CreateMeetingRoomBooking(MeetingBooking meetingRoom)
    {
        if (ValidateBookingRequest(meetingRoom) is { } validationError)
            return OperationResult<MeetingBooking>.Failure(validationError);

        meetingRoom.MeetingStartTime = meetingRoom.MeetingStartTime.ToUniversalTime();
        meetingRoom.MeetingEndDate = meetingRoom.MeetingEndDate.ToUniversalTime();
        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        if (!await _meetingRooms.ExistsAsync(meetingRoom.MeetingRoomId))
            return OperationResult<MeetingBooking>.Failure(
                new OperationError(OperationErrorCode.RoomNotFound, "Meeting room not found."));

        if (await _bookings.HasConflictAsync(
            meetingRoom.MeetingRoomId,
            meetingRoom.MeetingStartTime,
            meetingRoom.MeetingEndDate))
            return OperationResult<MeetingBooking>.Failure(
                new OperationError(OperationErrorCode.BookingConflict, "Meeting room already booked for the selected time slot."));

        meetingRoom.MeetingRoomIsBooked = true;
        _bookings.Add(meetingRoom);
        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();

        return OperationResult<MeetingBooking>.Success(meetingRoom);
    }

    public async Task<OperationResult<MeetingBooking>> UpdateMeetingRoomBooking(int bookingId, MeetingBooking meetingRoom)
    {
        if (ValidateBookingRequest(meetingRoom) is { } validationError)
            return OperationResult<MeetingBooking>.Failure(validationError);

        meetingRoom.MeetingStartTime = meetingRoom.MeetingStartTime.ToUniversalTime();
        meetingRoom.MeetingEndDate = meetingRoom.MeetingEndDate.ToUniversalTime();
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        var meetingRoomBooking = await _bookings.GetByIdAsync(bookingId);

        if (meetingRoomBooking is null)
            return OperationResult<MeetingBooking>.Failure(
                new OperationError(OperationErrorCode.BookingNotFound, "Booking does not exist."));

        if (!await _meetingRooms.ExistsAsync(meetingRoom.MeetingRoomId))
            return OperationResult<MeetingBooking>.Failure(
                new OperationError(OperationErrorCode.RoomNotFound, "Meeting room not found."));
        if (meetingRoomBooking.MeetingRoomIsBooked
            && await _bookings.HasConflictAsync(
                meetingRoom.MeetingRoomId,
                meetingRoom.MeetingStartTime,
                meetingRoom.MeetingEndDate,
                bookingId))
            return OperationResult<MeetingBooking>.Failure(
                new OperationError(OperationErrorCode.BookingConflict, "Meeting room already booked for the selected time slot."));

        meetingRoomBooking.MeetingRoomId = meetingRoom.MeetingRoomId;
        meetingRoomBooking.MeetingTitle = meetingRoom.MeetingTitle;
        meetingRoomBooking.MeetingStartTime = meetingRoom.MeetingStartTime;
        meetingRoomBooking.MeetingEndDate = meetingRoom.MeetingEndDate;

        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();

        return OperationResult<MeetingBooking>.Success(meetingRoomBooking);
    }

    public async Task<OperationResult<MeetingBooking>> CancelMeetingRoomBooking(int bookingId)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        var meetingRoomManager = await _bookings.GetByIdAsync(bookingId);

        if (meetingRoomManager is null)
            return OperationResult<MeetingBooking>.Failure(
                new OperationError(OperationErrorCode.BookingNotFound, "Booking does not exist."));

        if (meetingRoomManager.MeetingStartTime < DateTimeOffset.UtcNow)
            return OperationResult<MeetingBooking>.Failure(
                new OperationError(OperationErrorCode.MeetingAlreadyStarted, "Cannot cancel a meeting that has already started or finished."));

        meetingRoomManager.MeetingRoomIsBooked = false;
        await _unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();

        return OperationResult<MeetingBooking>.Success(meetingRoomManager);
    }

    private static OperationError? ValidateBookingRequest(MeetingBooking meetingRoom)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(meetingRoom, new ValidationContext(meetingRoom), validationResults, validateAllProperties: true);

        if (string.IsNullOrWhiteSpace(meetingRoom.MeetingTitle))
            validationResults.Add(new ValidationResult("Meeting title is required.", [nameof(meetingRoom.MeetingTitle)]));
        if (meetingRoom.MeetingStartTime >= meetingRoom.MeetingEndDate)
            validationResults.Add(new ValidationResult("Meeting start time must be before meeting end time.", [nameof(meetingRoom.MeetingStartTime), nameof(meetingRoom.MeetingEndDate)]));
        if (meetingRoom.MeetingStartTime < DateTimeOffset.UtcNow)
            validationResults.Add(new ValidationResult("Cannot book a room in the past.", [nameof(meetingRoom.MeetingStartTime)]));

        if (validationResults.Count == 0)
            return null;

        var errors = validationResults
            .SelectMany(result => result.MemberNames.Select(member => (member, result.ErrorMessage)))
            .GroupBy(error => error.member)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage ?? "Invalid value.").ToArray());

        return new OperationError(OperationErrorCode.Validation, "The booking is invalid.", errors);
    }
}