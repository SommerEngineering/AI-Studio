namespace AIStudio.Settings.DataModel;

public sealed class DataChat
{
    /// <summary>
    /// Shortcuts to send the input to the AI.
    /// </summary>
    public SendBehavior ShortcutSendBehavior { get; set; } = SendBehavior.MODIFER_ENTER_IS_SENDING;

    /// <summary>
    /// Preselect any chat options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// Should we preselect a provider for the chat?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}