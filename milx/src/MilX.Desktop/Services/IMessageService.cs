namespace MilX.Desktop.Services;

public enum DiscardChoice { Save, Discard, Cancel }

/// <summary>Modal prompts, kept behind an interface so view models stay free of windows.</summary>
public interface IMessageService
{
    Task<DiscardChoice> ConfirmDiscardAsync(string question);
    Task ShowErrorAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string okLabel = "OK");
}
