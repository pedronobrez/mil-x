using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CompMs.Common.DataObj;

namespace OpenDIAL.RawData.Plugins;

public sealed class RawReadOptions
{
    public bool GetProfileData { get; set; }
    public bool IsImagingMsData { get; set; }
    public bool IsGuiProcess { get; set; }
    public double PeakCutOff { get; set; }
    public Action<string>? Log { get; set; }
    public IProgress<double>? Progress { get; set; }
}

/// <summary>
/// Extension point for additional readers (for example a Thermo RawFileReader based reader
/// that needs a separately licensed library). Assemblies in "&lt;app&gt;/plugins" and
/// "$OPENDIAL_PLUGINS" that contain implementations are loaded lazily on first use.
/// </summary>
public interface IRawFileReaderPlugin
{
    string Name { get; }
    /// <summary>Lower numbers win when several plugins can read the same file.</summary>
    int Priority { get; }
    bool CanRead(string path);
    RawMeasurement Read(string path, int fileId, RawReadOptions options);
}

public static class RawReaderPlugins
{
    private static readonly object Sync = new();
    private static List<IRawFileReaderPlugin>? _plugins;
    private static readonly List<IRawFileReaderPlugin> Registered = new();

    public static void Register(IRawFileReaderPlugin plugin) {
        lock (Sync) {
            Registered.Add(plugin);
            _plugins = null;
        }
    }

    public static IReadOnlyList<IRawFileReaderPlugin> All {
        get {
            lock (Sync) {
                if (_plugins == null) {
                    var list = new List<IRawFileReaderPlugin>(Registered);
                    foreach (var dir in PluginDirectories()) {
                        list.AddRange(LoadFrom(dir));
                    }
                    list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                    _plugins = list;
                }
                return _plugins;
            }
        }
    }

    public static IRawFileReaderPlugin? Find(string path) {
        foreach (var plugin in All) {
            try {
                if (plugin.CanRead(path)) return plugin;
            }
            catch { }
        }
        return null;
    }

    private static IEnumerable<string> PluginDirectories() {
        var env = Environment.GetEnvironmentVariable("OPENDIAL_PLUGINS");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env)) yield return env!;
        var local = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (Directory.Exists(local)) yield return local;
    }

    private static IEnumerable<IRawFileReaderPlugin> LoadFrom(string dir) {
        var result = new List<IRawFileReaderPlugin>();
        foreach (var dll in Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories)) {
            Assembly asm;
            try {
                asm = Assembly.LoadFrom(dll);
            }
            catch {
                continue;
            }
            Type[] types;
            try {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex) {
                types = Array.FindAll(ex.Types, t => t != null)!;
            }
            foreach (var type in types) {
                if (type.IsAbstract || !typeof(IRawFileReaderPlugin).IsAssignableFrom(type)) continue;
                try {
                    if (Activator.CreateInstance(type) is IRawFileReaderPlugin plugin) result.Add(plugin);
                }
                catch { }
            }
        }
        return result;
    }
}
