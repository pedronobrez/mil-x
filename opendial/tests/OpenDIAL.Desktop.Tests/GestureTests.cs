using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using OpenDIAL.Desktop.Views;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// The keyboard shortcuts, pressed rather than read.
///
/// There is a trap in how a gesture is written. "Cmd+5" parses without complaint and binds
/// <see cref="Key.Clear"/>, because the parser reads the digit as the numeric value of the key
/// enumeration rather than as the digit key; "Cmd+1" binds <see cref="Key.Cancel"/> the same way.
/// Nothing warns, nothing throws, and the shortcut simply never fires. Every workspace shortcut in
/// the window was written that way and none of them worked.
/// </summary>
public class GestureTests
{
    [Theory]
    [InlineData("Cmd+D1", Key.D1)]
    [InlineData("Cmd+D5", Key.D5)]
    [InlineData("Ctrl+D3", Key.D3)]
    public void A_digit_shortcut_has_to_be_written_as_a_digit_key(string text, Key expected)
    {
        Assert.Equal(expected, KeyGesture.Parse(text).Key);
    }

    [Theory]
    [InlineData("Cmd+1", Key.Cancel)]
    [InlineData("Cmd+5", Key.Clear)]
    public void The_bare_digit_binds_something_else_entirely(string text, Key wrong)
    {
        // written down so the next person meets this on purpose rather than by accident
        Assert.Equal(wrong, KeyGesture.Parse(text).Key);
    }

    [AvaloniaFact]
    public void The_menu_shows_a_digit_shortcut_as_a_digit()
    {
        // D1 is how the gesture has to be written; it is not how the reader should see it. With the
        // application running, the platform converter turns it back into "1".
        var shown = Avalonia.Controls.Converters.PlatformKeyGestureConverter.ToPlatformString(KeyGesture.Parse("Cmd+D1"));
        Assert.DoesNotContain("D1", shown);
        Assert.Contains("1", shown);
    }

    [AvaloniaFact]
    public void No_menu_item_advertises_a_shortcut_that_reads_wrong()
    {
        var window = new MainWindow();
        var gestures = window.GetLogicalDescendants().OfType<MenuItem>().Where(m => m.InputGesture is not null).ToList();
        Assert.NotEmpty(gestures);
        foreach (var item in gestures)
        {
            var shown = Avalonia.Controls.Converters.PlatformKeyGestureConverter.ToPlatformString(item.InputGesture!);
            Assert.DoesNotContain("D1", shown);
            Assert.DoesNotContain("Cancel", shown);
            Assert.DoesNotContain("Clear", shown);
        }
    }

    [AvaloniaFact]
    public void Every_shortcut_in_the_main_window_binds_the_key_it_names()
    {
        var window = new MainWindow();
        Assert.NotEmpty(window.KeyBindings);
        foreach (var binding in window.KeyBindings)
        {
            var gesture = binding.Gesture;
            Assert.NotNull(gesture);
            Assert.NotEqual(Key.None, gesture!.Key);
            // no shortcut in this application means to bind any of these
            Assert.False(gesture.Key is Key.Cancel or Key.Clear or Key.Back or Key.Tab or Key.LineFeed,
                $"{gesture} binds {gesture.Key}, which is what a bare digit parses to");
        }
    }

    [AvaloniaFact]
    public void The_workspace_shortcuts_actually_switch_workspace()
    {
        var vm = new ViewModels.MainWindowViewModel(
            new Services.SettingsService(),
            new NoDialogs(),
            new NoMessages());
        var window = new MainWindow { DataContext = vm, Width = 900, Height = 600 };
        window.Show();

        // the fifth workspace is the Statistics one; the first is the Explorer
        window.KeyPress(Key.D5, RawInputModifiers.Meta, PhysicalKey.Digit5, "5");
        Assert.Equal(4, vm.SelectedWorkspace);

        window.KeyPress(Key.D1, RawInputModifiers.Meta, PhysicalKey.Digit1, "1");
        Assert.Equal(0, vm.SelectedWorkspace);

        // and the same on the control key, for a keyboard without a command key
        window.KeyPress(Key.D3, RawInputModifiers.Control, PhysicalKey.Digit3, "3");
        Assert.Equal(2, vm.SelectedWorkspace);

        window.Close();
    }

    private sealed class NoDialogs : Services.IFileDialogService
    {
        public Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns, bool allowMultiple, string? startFolder = null)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<string?> PickFolderAsync(string title, string? startFolder = null) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string extension, string? startFolder = null) => Task.FromResult<string?>(null);
    }

    private sealed class NoMessages : Services.IMessageService
    {
        public Task<Services.DiscardChoice> ConfirmDiscardAsync(string question) => Task.FromResult(Services.DiscardChoice.Discard);
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string okLabel = "OK") => Task.FromResult(true);
    }
}
