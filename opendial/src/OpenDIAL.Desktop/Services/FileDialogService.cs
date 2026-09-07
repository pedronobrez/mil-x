using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace OpenDIAL.Desktop.Services;

public interface IFileDialogService
{
    Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns, bool allowMultiple, string? startFolder = null);
    Task<string?> PickFolderAsync(string title, string? startFolder = null);
    Task<string?> SaveFileAsync(string title, string suggestedName, string extension, string? startFolder = null);
}

/// <summary>Thin wrapper over Avalonia's StorageProvider so view models stay free of Avalonia types.</summary>
public sealed class FileDialogService : IFileDialogService
{
    private readonly Window _owner;

    public FileDialogService(Window owner)
    {
        _owner = owner;
    }

    private async Task<IStorageFolder?> StartAsync(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return null;
        }
        try
        {
            return await _owner.StorageProvider.TryGetFolderFromPathAsync(folder);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns, bool allowMultiple, string? startFolder = null)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            SuggestedStartLocation = await StartAsync(startFolder),
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Mass spectrometry data") { Patterns = patterns },
                FilePickerFileTypes.All,
            },
        });
        return files.Select(f => f.TryGetLocalPath()).Where(p => p is not null).Select(p => p!).ToList();
    }

    public async Task<string?> PickFolderAsync(string title, string? startFolder = null)
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await StartAsync(startFolder),
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> SaveFileAsync(string title, string suggestedName, string extension, string? startFolder = null)
    {
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            SuggestedStartLocation = await StartAsync(startFolder),
            FileTypeChoices = new[] { new FilePickerFileType(extension.ToUpperInvariant() + " file") { Patterns = new[] { "*." + extension } } },
        });
        return file?.TryGetLocalPath();
    }
}
