using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MilX.Cli.Commands;

namespace MilX.Cli;

/// <summary>
/// The dispatcher: a command name and its arguments in, a <see cref="Document"/> out, whether the
/// request came from the command line, a request file in the inbox, or a line on stdin. Printing is
/// the caller's; the document is the same bytes whichever way it was asked for.
/// </summary>
public static class Cli
{
    public static string Version { get; } = Read();

    private static string Read()
    {
        var informational = typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }
        return typeof(Cli).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>The contract's subcommands, the ones a request file may name.</summary>
    public static readonly IReadOnlySet<string> ContractCommands = new HashSet<string>(StringComparer.Ordinal)
    {
        "selftest", "info", "scans", "xic", "spectrum", "isotopes", "batch-xic", "convert", "export",
    };

    /// <summary>The MS-DIAL console's workflows, passed through in-process.</summary>
    public static readonly IReadOnlySet<string> MsdialCommands = new HashSet<string>(StringComparer.Ordinal)
    {
        "lcms", "gcms", "dims", "imms", "lcimms", "msn", "eic",
    };

    public static async Task<int> MainAsync(string[] argv, TextWriter stdout, TextWriter stderr)
    {
        if (argv.Length == 0 || argv[0] is "--help" or "-h" or "help")
        {
            stdout.Write(Help());
            return 0;
        }
        if (argv[0] is "--version")
        {
            stdout.WriteLine(Version);
            return 0;
        }
        var command = argv[0];
        var args = Args.Parse(argv.Skip(1).ToList());
        var quiet = args.Flag("quiet");
        var store = new RawStore(args.Get("cache"));
        if (!quiet) store.Log = line => stderr.WriteLine(line);
        try
        {
            if (command == "serve")
            {
                return await Serve.RunAsync(args, store, stdout, stderr, quiet);
            }
            if (MsdialCommands.Contains(command))
            {
                var doc = await Msdial.RunAsync(command, args, stderr, quiet);
                if (doc is not null) stdout.Write(doc.Render(args.Get("format", "json")));
                return doc is null ? 0 : 0;
            }
            var document = await RunAsync(command, args, store, stderr, quiet, CancellationToken.None);
            stdout.Write(document.Render(args.Get("format", "json")));
            return 0;
        }
        catch (CliError e)
        {
            stderr.Write(ErrorDocument(e.Name, e.Message));
            return e.ExitCode;
        }
        catch (OperationCanceledException)
        {
            stderr.Write(ErrorDocument("timeout", "the command was cancelled"));
            return (int)ErrorCode.Internal;
        }
        catch (Exception e)
        {
            stderr.Write(ErrorDocument("internal", e.GetType().Name + ": " + e.Message));
            if (!quiet) stderr.WriteLine(e.ToString());
            return (int)ErrorCode.Internal;
        }
    }

    /// <summary>Runs one contract command. Throws <see cref="CliError"/> for anything the contract names.</summary>
    public static async Task<Document> RunAsync(string command, Args args, RawStore store, TextWriter? diagnostics, bool quiet, CancellationToken ct)
    {
        var log = quiet || diagnostics is null ? (Action<string>)(_ => { }) : diagnostics.WriteLine;
        return command switch
        {
            "selftest" => Selftest.Run(args, store, log),
            "info" => Info.Run(args, store, ct),
            "scans" => Scans.Run(args, store, ct),
            "xic" => Xic.Run(args, store, ct),
            "spectrum" => Spectrum.Run(args, store, ct),
            "isotopes" => Isotopes.Run(args, store, ct),
            "batch-xic" => BatchXic.Run(args, store, log, ct),
            "convert" => Commands.Convert.Run(args, store, ct),
            "export" => await Export.RunAsync(args, ct),
            _ when MsdialCommands.Contains(command) => await Msdial.RunAsync(command, args, diagnostics, quiet) ?? throw new CliError(ErrorCode.Internal, $"{command} produced no document"),
            _ => throw new CliError(ErrorCode.BadArguments, $"unknown command '{command}'; the commands are {string.Join(", ", ContractCommands.Concat(MsdialCommands).Append("serve"))}", "not_allowed"),
        };
    }

    public static string ErrorDocument(string code, string message)
    {
        var o = new JsonObject { ["schema"] = Document.Schema, ["error"] = code, ["message"] = message };
        return o.ToJsonString(Document.Compact) + "\n";
    }

    public static string Help() => $"""
        milx {Version} — the MIL suite's command line (mil-cli/1); see docs/CLI-CONTRACT.md

          milx selftest [FILES…]                        what works on this machine, and whether each file reads
          milx info FILE [--sample N|NAME]              samples and channels
          milx scans FILE [--source S] [--rt A:B] [--parent MZ[:TOL]] [--ms-level N] [--count]
          milx xic FILE --mz LIST [--tol 0.01] [--unit Da|ppm] [--source S] [--rt A:B] [--smooth N] [--trace] [--reference MZ@SOURCE]
          milx spectrum FILE [--source S] (--scan N | --rt A:B) [--subtract A:B]… [--centroid vendor|local|none] [--top N] [--min-rel 0.01] [--msp PATH]
          milx isotopes FILE --mz MZ [--charge Z] [--rt A:B] [--formula F] [--tol 0.01]
          milx batch-xic PATHS… --mz LIST [--rt-window C±W] (as xic)
          milx convert FILE --out OUT.mzML [--centroid vendor|local|none]
          milx export --project P (--reviewed OUT | --matrix OUT | --milq OUT) [--areas] [--annotated-only]
          milx serve (--stdio | --inbox DIR) [--allow-root DIR]… [--max-jobs 1] [--idle-exit MIN]
          milx lcms -i IN -o OUT -m METHOD [-p] [--acquisition-type DDA|SWATH] [--library X.msp] [--summary]
          milx gcms|dims|imms|lcimms|msn|eic …          the MS-DIAL console's own workflows, unchanged

        Sources: ms1 · channel:N · ms2-all · gated:MZ[:TOL] (MS2 scans whose own precursor is within TOL of MZ, default 0.05 Da).
        Common flags: --format json|tsv, --quiet, --cache DIR, --sample N|NAME, --require (an empty result is an error).
        Errors are JSON on stderr; exit 2 bad arguments, 3 unreadable, 4 nothing found, 5 internal.

        """;
}
