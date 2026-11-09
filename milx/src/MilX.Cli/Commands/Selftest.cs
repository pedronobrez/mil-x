using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using CompMs.RawDataHandler.Core;
using MilX.Pipeline;
using MilX.Pipeline.Vendor;
using MilX.Plugins.SciexWiff;

namespace MilX.Cli.Commands;

/// <summary>What works on this machine: the platform, the runtime, the vendor SDK, msconvert, and whether each file given reads.</summary>
internal static class Selftest
{
    public static Document Run(Args args, RawStore store, Action<string> log)
    {
        var doc = new Document("selftest");
        doc.Root["platform"] = new JsonObject
        {
            ["os"] = RuntimeInformation.OSDescription,
            ["architecture"] = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["process_architecture"] = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            ["self_contained"] = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT")) && !File.Exists(Path.Combine(AppContext.BaseDirectory, "milx.runtimeconfig.json")),
            ["invariant_culture"] = System.Globalization.CultureInfo.CurrentCulture.Name.Length == 0,
        };
        doc.Root["upstream"] = "MS-DIAL " + PipelineRunner.UpstreamVersion;

        bool sdk;
        string sdkNote;
        try
        {
            sdk = WiffSupport.IsNativeAvailable;
            sdkNote = sdk ? "SCIEX Clearcore2 loaded: .wiff is read natively" : "SCIEX Clearcore2 not found beside the binary (plugins/sciex): .wiff goes through msconvert";
        }
        catch (Exception e) { sdk = false; sdkNote = "the SCIEX plugin failed to load: " + e.Message; }
        var pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins", "sciex");
        doc.Root["vendor_sdk"] = new JsonObject
        {
            ["sciex_wiff"] = sdk,
            ["plugin_folder"] = Directory.Exists(pluginDir) ? pluginDir : null,
            ["note"] = sdkNote,
        };
        var converter = RawDataAccessOptions.Vendor.Detect();
        doc.Root["msconvert"] = new JsonObject
        {
            ["available"] = converter.IsAvailable,
            ["kind"] = converter.Kind.ToString().ToLowerInvariant(),
            ["executable"] = converter.Executable,
            ["docker_image"] = converter.DockerImage,
            ["note"] = converter.Message,
        };
        doc.Root["cache"] = store.CacheRoot;
        doc.Root["formats"] = new JsonArray(FileFormats.AllExtensions.Select(e => (JsonNode)e).ToArray());

        var files = new JsonArray();
        var paths = args.Positional.Count > 0 ? args.Positional : args.All("file").ToList();
        if (paths.Count > 0)
        {
            var report = FolderCheck.Check(paths);
            foreach (var f in report.Findings)
            {
                files.Add(new JsonObject { ["path"] = f.Path, ["ok"] = false, ["finding"] = f.Finding, ["action"] = f.Action, ["kind"] = Common.KindName(f.Kind) });
            }
            foreach (var path in report.Paths)
            {
                var entry = new JsonObject { ["path"] = path };
                try
                {
                    var samples = RawStore.SampleNames(path);
                    var open = store.Open(path, 0);
                    entry["ok"] = true;
                    entry["samples"] = samples.Count;
                    entry["channels"] = open.Channels.Count;
                    entry["spectra"] = open.Spectra;
                    entry["ms1_spectra"] = open.Raw.SpectrumList.Count(s => s.MsLevel <= 1);
                    entry["read_seconds"] = Math.Round(open.ReadTime.TotalSeconds, 2);
                    entry["reader"] = WiffSupport.CanReadNatively(path) ? "sciex-native" : FileFormats.IsVendorFormat(path) ? "msconvert" : "mzml";
                }
                catch (CliError e) { entry["ok"] = false; entry["error"] = e.Message; }
                catch (Exception e) { entry["ok"] = false; entry["error"] = e.GetType().Name + ": " + e.Message; }
                files.Add(entry);
            }
        }
        doc.Root["files"] = files;
        doc.Root["ok"] = files.All(f => f!["ok"]?.GetValue<bool>() != false);
        return doc;
    }
}
