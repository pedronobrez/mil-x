using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Pipeline;
using OpenDIAL.Pipeline.Model;

namespace OpenDIAL.Desktop.ViewModels;

public sealed partial class RunViewModel : ViewModelBase
{
    private const int MaxLogLines = 5000;
    private CancellationTokenSource? _cts;

    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _stage = "Idle";
    [ObservableProperty] private string _currentFile = string.Empty;
    [ObservableProperty] private string _statusText = "No run yet. Configure a project and press Run.";
    [ObservableProperty] private PipelineResult? _lastResult;
    [ObservableProperty] private string? _lastError;

    /// <summary>Raised on the UI thread when a run finishes successfully.</summary>
    public event EventHandler<PipelineResult>? Completed;

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel()
    {
        _cts?.Cancel();
        StatusText = "Cancelling…";
    }

    partial void OnIsRunningChanged(bool value) => CancelCommand.NotifyCanExecuteChanged();

    public async Task<PipelineResult?> RunAsync(PipelineRequest request)
    {
        if (IsRunning) return null;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        Progress = 0;
        Stage = "Starting";
        CurrentFile = string.Empty;
        LastError = null;
        LastResult = null;
        LogLines.Clear();
        StatusText = "Running…";

        var progress = new UiProgress(this);
        try
        {
            var runner = new PipelineRunner();
            var token = _cts.Token;
            var result = await Task.Run(() => runner.RunAsync(request, progress, token), token);
            LastResult = result;
            Progress = 100;
            Stage = "Done";
            StatusText = $"Finished in {result.Elapsed:mm\\:ss}. {result.ExportedFiles.Count} files exported to {result.OutputFolder}.";
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Stage = "Cancelled";
            StatusText = "Run cancelled.";
            Append("Run cancelled by user.");
            return null;
        }
        catch (Exception ex)
        {
            Stage = "Failed";
            LastError = ex.Message;
            StatusText = "Run failed: " + ex.Message;
            Append("ERROR: " + ex);
            return null;
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void Append(string line)
    {
        LogLines.Add($"{DateTime.Now:HH:mm:ss}  {line}");
        if (LogLines.Count > MaxLogLines)
        {
            LogLines.RemoveAt(0);
        }
    }

    private void Apply(PipelineProgress p)
    {
        Progress = Math.Clamp(p.OverallPercent, 0, 100);
        Stage = p.Stage;
        CurrentFile = p.FileName ?? string.Empty;
        if (p.ShowInLog)
        {
            Append(p.FileName is null ? $"[{p.Stage}] {p.Message}" : $"[{p.Stage}] {p.FileName}: {p.Message}");
        }
    }

    /// <summary>Marshals pipeline callbacks (raised on worker threads) to the UI thread.</summary>
    private sealed class UiProgress : IProgress<PipelineProgress>
    {
        private readonly RunViewModel _vm;
        public UiProgress(RunViewModel vm) => _vm = vm;
        public void Report(PipelineProgress value) => Dispatcher.UIThread.Post(() => _vm.Apply(value), DispatcherPriority.Background);
    }
}
