namespace AlyaOfflineChat.Models;

public sealed class ChatMessage
{
    public required string Sender { get; set; }
    public required string Content { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
