namespace Valuator.Services;

public sealed class CalculationEventMessage
{
    public string EventType { get; set; } = string.Empty;
    public string TextId { get; set; } = string.Empty;
    public double? Rank { get; set; }
    public int? Similarity { get; set; }
}