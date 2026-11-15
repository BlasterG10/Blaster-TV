namespace MagisWindowsNative.Models;

public sealed class EpgProgram
{
    public string ChannelId { get; set; } = "";
    public string ChannelName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime Stop { get; set; }
    public string TimeRange => $"{Start:MM-dd HH:mm} – {Stop:HH:mm}";
}
