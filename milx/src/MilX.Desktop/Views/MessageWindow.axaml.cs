using Avalonia.Controls;
using MilX.Desktop.Services;

namespace MilX.Desktop.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message, params (string Label, object Result, bool Primary)[] buttons) : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        foreach (var (label, result, primary) in buttons)
        {
            var b = new Button { Content = label, MinWidth = 90 };
            if (primary) b.Classes.Add("primary");
            b.Click += (_, _) => Close(result);
            Buttons.Children.Add(b);
        }
    }
}

/// <summary>Modal prompts on top of the main window.</summary>
public sealed class MessageService : IMessageService
{
    private readonly Window _owner;
    public MessageService(Window owner) => _owner = owner;

    public async Task<DiscardChoice> ConfirmDiscardAsync(string question)
    {
        var w = new MessageWindow("Save this project first?", question, ("Cancel", DiscardChoice.Cancel, false), ("Discard", DiscardChoice.Discard, false), ("Save", DiscardChoice.Save, true));
        var r = await w.ShowDialog<object?>(_owner);
        return r is DiscardChoice c ? c : DiscardChoice.Cancel;
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        await new MessageWindow(title, message, ("OK", true, true)).ShowDialog<object?>(_owner);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string okLabel = "OK")
    {
        var r = await new MessageWindow(title, message, ("Cancel", false, false), (okLabel, true, true)).ShowDialog<object?>(_owner);
        return r is true;
    }
}
