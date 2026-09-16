using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MilX.Desktop.Services;
using MilX.Desktop.ViewModels;
using MilX.Desktop.Views;
using Xunit;

namespace MilX.Desktop.Tests;

/// <summary>
/// The probe is what the smoke test reads to decide whether the packaged application did what it
/// was asked. That makes its shape a contract: a rename here fails a script nobody runs on every
/// commit, so it is held here instead.
/// </summary>
public class UiProbeTests
{
    [AvaloniaFact]
    public void It_writes_nothing_unless_the_environment_asks()
    {
        var was = Environment.GetEnvironmentVariable("MILX_UI_PROBE");
        try
        {
            Environment.SetEnvironmentVariable("MILX_UI_PROBE", null);
            Assert.Null(UiProbe.FromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MILX_UI_PROBE", was);
        }
    }

    [AvaloniaFact]
    public void It_says_what_the_window_is_showing_and_where_its_tabs_are()
    {
        var path = Path.Combine(Path.GetTempPath(), "milx-probe-" + Guid.NewGuid().ToString("N") + ".json");
        var was = Environment.GetEnvironmentVariable("MILX_UI_PROBE");
        try
        {
            Environment.SetEnvironmentVariable("MILX_UI_PROBE", path);
            var probe = UiProbe.FromEnvironment();
            Assert.NotNull(probe);

            var vm = new MainWindowViewModel(new SettingsService(), new NoDialogs(), new NoMessages());
            var window = new MainWindow { DataContext = vm, Width = 1200, Height = 800 };
            window.Show();
            vm.SelectedWorkspace = 4;
            probe!.Write(window, vm);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            Assert.Equal(4, root.GetProperty("workspace").GetInt32());
            Assert.Equal("Statistics", root.GetProperty("workspaceName").GetString());
            Assert.False(root.GetProperty("hasResults").GetBoolean());
            Assert.Equal(0, root.GetProperty("ionRows").GetInt32());
            Assert.Contains("MIL-X", root.GetProperty("title").GetString());
            Assert.True(root.GetProperty("client").GetProperty("width").GetDouble() > 0);

            // where the tabs are, inside the window, so a script can turn that into a click
            var tabs = root.GetProperty("controls");
            foreach (var name in new[] { "Explorer", "Analytics", "Method", "Samples", "Statistics" })
            {
                var tab = tabs.GetProperty("workspace." + name);
                Assert.True(tab.GetProperty("width").GetDouble() > 0, name);
                Assert.True(tab.GetProperty("x").GetDouble() > 0, name);
            }

            // and they are in the order they are drawn in
            var xs = new[] { "Explorer", "Analytics", "Method", "Samples", "Statistics" }
                .Select(n => tabs.GetProperty("workspace." + n).GetProperty("x").GetDouble())
                .ToList();
            Assert.Equal(xs.OrderBy(v => v), xs);

            window.Close();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MILX_UI_PROBE", was);
            try { File.Delete(path); } catch { }
        }
    }

    private sealed class NoDialogs : IFileDialogService
    {
        public Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns, bool allowMultiple, string? startFolder = null)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<string?> PickFolderAsync(string title, string? startFolder = null) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string extension, string? startFolder = null) => Task.FromResult<string?>(null);
    }

    private sealed class NoMessages : IMessageService
    {
        public Task<DiscardChoice> ConfirmDiscardAsync(string question) => Task.FromResult(DiscardChoice.Discard);
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string okLabel = "OK") => Task.FromResult(true);
    }
}
