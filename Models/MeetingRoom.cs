// Represents a bookable meeting room.
public class MeetingRoom
{
    public int Id { get; set; }
    
    public required string Name { get; set; }

    public int Capacity { get; set; }
}