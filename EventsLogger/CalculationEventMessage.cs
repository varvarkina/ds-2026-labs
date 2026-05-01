namespace EventsLogger;

public sealed class CalculationEventMessage
{
    public string EventType { get; set; } = "";
    public string TextId { get; set; } = "";
    public double? Rank { get; set; }
    public int? Similarity { get; set; }
}
