using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

// Represents a meeting booking and its room, title, time range, and active state.
public class MeetingBooking
{
    public int Id { get; set; }

    [Range(1, int.MaxValue)]
    public required int MeetingRoomId { get; set; }

    public bool MeetingRoomIsBooked { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string MeetingTitle { get; set; }

    [JsonConverter(typeof(OffsetDateTimeJsonConverter))]
    public DateTimeOffset MeetingStartTime { get; set; }

    [JsonConverter(typeof(OffsetDateTimeJsonConverter))]
    public DateTimeOffset MeetingEndDate { get; set; }
}