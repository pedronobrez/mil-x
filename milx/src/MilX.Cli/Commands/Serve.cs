using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MilX.Cli.Commands;

/// <summary>
/// Keeps files open and answers requests: JSON lines on stdin (<c>--stdio</c>), or a watched folder
/// (<c>--inbox DIR</c>) — the way an agent that cannot type into a terminal reaches the Mac. A request
/// is <c>inbox/&lt;id&gt;.json</c>; the server moves it to <c>running/</c>, executes it in-process on the
/// same code the command line runs, writes <c>outbox/&lt;id&gt;.json</c> atomically — byte for byte what
/// the command line would have printed — and keeps the request beside it, or puts both in
/// <c>failed/</c> with the error document. An allow-list, not a shell: only the contract's commands
/// and the MS-DIAL workflow; every path must resolve inside an <c>--allow-root</c> or the inbox folder;
/// one job at a time by default; a per-job timeout; STATUS.json and log.jsonl; STOP to leave.
/// </summary>
internal static class Serve
{
    public static async Task<int> RunAsync(Args args, RawStore store, TextWriter stdout, TextWriter stderr, bool quiet)
    {
        if (args.Flag("stdio")) return await StdioAsync(args, store, stdout, stderr, quiet);
        var inbox = args.Get("inbox") ?? throw new CliError(ErrorCode.BadArguments, "serve needs --stdio or --inbox DIR");
        var server = new InboxServer(Path.GetFullPath(inbox), args.All("allow-root").Select(Path.GetFullPath).ToList(), store)
        {
            MaxJobs = Math.Max(1, args.Int("max-jobs", 1)),
            IdleExit = args.Get("idle-exit") is { } m ? TimeSpan.FromMinutes(double.Parse(m, System.Globalization.CultureInfo.InvariantCulture)) : null,
            DefaultTimeout = TimeSpan.FromSeconds(args.Double("timeout", 1800)),
            Log = quiet ? _ => { } : stderr.WriteLine,
            AllowMsdial = !args.Flag("no-msdial"),
        };
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        await server.RunAsync(cts.Token);
        return 0;
    }

    private static async Task<int> StdioAsync(Args args, RawStore store, TextWriter stdout, TextWriter stderr, bool quiet)
    {
        var roots = args.All("allow-root").Select(Path.GetFullPath).ToList();
        var runner = new JobRunner(store, roots, null, TimeSpan.FromSeconds(args.Double("timeout", 1800))) { AllowMsdial = !args.Flag("no-msdial") };
        string? line;
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            if (line.Trim().Length == 0) continue;
            var (ok, text, _) = await runner.ExecuteAsync(line, null, CancellationToken.None);
            stdout.Write(text.EndsWith('\n') ? text : text + "\n");
            if (!ok && !quiet) stderr.WriteLine("[serve] request failed");
        }
        return 0;
    }
}

/// <summary>One request from a file or a line: parsed, checked against the allow-list and the roots, run.</summary>
internal sealed class JobRunner
{
    private readonly RawStore _store;
    private readonly List<string> _roots;
    private readonly string? _inboxRoot;
    private readonly TimeSpan _defaultTimeout;

    public JobRunner(RawStore store, List<string> roots, string? inboxRoot, TimeSpan defaultTimeout)
    {
        _store = store;
        _roots = roots;
        _inboxRoot = inboxRoot;
        _defaultTimeout = defaultTimeout;
    }

    public bool AllowMsdial { get; set; } = true;
    /// <summary>For the tests: a command that sleeps, to exercise the timeout.</summary>
    internal bool AllowSleepForTests { get; set; }

    /// <summary>The argument names that carry paths, checked against the roots: inputs must be inside them; outputs inside them too, and never over an existing file outside the outbox.</summary>
    private static readonly HashSet<string> InputPathArgs = new(StringComparer.Ordinal) { "file", "files", "project", "library", "i", "input", "m", "method" };
    private static readonly HashSet<string> OutputPathArgs = new(StringComparer.Ordinal) { "out", "msp", "reviewed", "matrix", "milq", "o", "output", "cache" };

    /// <summary>Runs a request given as JSON text. Answers (ok, the document text as the CLI prints it, the files written).</summary>
    public async Task<(bool Ok, string Text, List<string> Files)> ExecuteAsync(string requestJson, string? outputDir, CancellationToken ct)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(requestJson); }
        catch (JsonException e) { return (false, Cli.ErrorDocument("bad_arguments", "the request is not JSON: " + e.Message), new()); }
        if (node is not JsonObject request) return (false, Cli.ErrorDocument("bad_arguments", "the request must be a JSON object"), new());
        if (request["batch"] is JsonArray batch)
        {
            // several requests in one file, run in order; the answer is one document per request
            var answers = new JsonArray();
            var files = new List<string>();
            var allOk = true;
            foreach (var item in batch)
            {
                var (ok, text, written) = await ExecuteAsync(item?.ToJsonString() ?? "{}", outputDir, ct);
                allOk &= ok;
                files.AddRange(written);
                answers.Add(JsonNode.Parse(text));
            }
            var envelope = new JsonObject { ["schema"] = Document.Schema, ["tool"] = Document.Tool, ["version"] = Cli.Version, ["command"] = "batch", ["id"] = request["id"]?.DeepClone(), ["ok"] = allOk, ["answers"] = answers };
            return (allOk, envelope.ToJsonString(Document.Pretty) + "\n", files);
        }
        var command = request["command"]?.GetValue<string>() ?? string.Empty;
        var allowed = Cli.ContractCommands.Contains(command) || (AllowMsdial && Cli.MsdialCommands.Contains(command)) || (AllowSleepForTests && command == "_sleep");
        if (!allowed) return (false, Cli.ErrorDocument("not_allowed", $"'{command}' is not a command this server runs; the allow-list is {string.Join(", ", Cli.ContractCommands.Concat(AllowMsdial ? Cli.MsdialCommands : Array.Empty<string>()))}"), new());
        var args = ToArgs(request["args"] as JsonObject, request["format"]?.GetValue<string>());
        try
        {
            args = CheckPaths(command, args, outputDir);
        }
        catch (CliError e) { return (false, Cli.ErrorDocument(e.Name, e.Message), new()); }
        var timeout = request["timeout_s"] is JsonValue tv && tv.TryGetValue<double>(out var secs) && secs > 0 ? TimeSpan.FromSeconds(secs) : _defaultTimeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            if (command == "_sleep")
            {
                await Task.Delay(TimeSpan.FromSeconds(args.Double("seconds", 5)), cts.Token);
                return (true, new Document("_sleep").ToJson(), new());
            }
            var task = Cli.RunAsync(command, args, _store, null, true, cts.Token);
            var finished = await Task.WhenAny(task, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token).ContinueWith(_ => (Document?)null, TaskScheduler.Default));
            if (finished != task || cts.IsCancellationRequested) return (false, Cli.ErrorDocument("timeout", $"the job did not finish in {timeout.TotalSeconds:F0} s"), new());
            var doc = await task;
            // large outputs go to files beside the answer, and the answer lists them
            var text = doc.Render(args.Get("format", "json"));
            if (outputDir is not null && (args.Flag("trace") || args.Get("format", "json") == "tsv"))
            {
                Directory.CreateDirectory(outputDir);
                var path = Path.Combine(outputDir, command + (args.Get("format", "json") == "tsv" ? ".tsv" : ".trace.json"));
                WriteAtomic(path, text);
                doc.Files.Insert(0, path);
                if (args.Flag("trace"))
                {
                    // the summary stays in the answer; the points are in the file
                    if (doc.Root["xics"] is JsonArray xics) foreach (var x in xics) (x as JsonObject)?.Remove("trace");
                }
                doc.Root["files"] = new JsonArray(doc.Files.Select(f => (JsonNode)f).ToArray());
                text = doc.ToJson();
            }
            else if (doc.Files.Count > 0)
            {
                doc.Root["files"] = new JsonArray(doc.Files.Select(f => (JsonNode)f).ToArray());
                text = doc.Render(args.Get("format", "json"));
            }
            return (true, text, doc.Files);
        }
        catch (CliError e) { return (false, Cli.ErrorDocument(e.Name, e.Message), new()); }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested) { return (false, Cli.ErrorDocument("timeout", $"the job did not finish in {timeout.TotalSeconds:F0} s"), new()); }
        catch (OperationCanceledException) { return (false, Cli.ErrorDocument("interrupted", "the server stopped while the job ran"), new()); }
        catch (Exception e) { return (false, Cli.ErrorDocument("internal", e.GetType().Name + ": " + e.Message), new()); }
    }

    /// <summary>The request's args object as command-line arguments: names without dashes, values as strings, arrays as repeated flags.</summary>
    internal static Args ToArgs(JsonObject? argsObject, string? format)
    {
        var args = new Args();
        if (argsObject is not null)
        {
            foreach (var (name, value) in argsObject)
            {
                switch (value)
                {
                    case null: break;
                    case JsonArray array:
                        foreach (var v in array) if (v is not null) args.Add(name, Scalar(v));
                        break;
                    default: args.Add(name, Scalar(value)); break;
                }
            }
        }
        if (!string.IsNullOrEmpty(format)) args.Add("format", format);
        return args;
    }

    private static string Scalar(JsonNode v) => v is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : v.ToJsonString().Trim('"');

    /// <summary>Every path in the request resolves (symlinks followed) inside a root or the inbox; outputs never write over a file outside the outbox.</summary>
    private Args CheckPaths(string command, Args args, string? outputDir)
    {
        if (_roots.Count == 0 && _inboxRoot is null) return args;   // stdio with no roots: the caller is local and trusted
        foreach (var name in args.Names.ToList())
        {
            var isInput = InputPathArgs.Contains(name);
            var isOutput = OutputPathArgs.Contains(name);
            if (!isInput && !isOutput) continue;
            foreach (var value in args.All(name))
            {
                if (value == "true") continue;
                var full = Path.GetFullPath(value);
                var resolved = ResolveLinks(full);
                if (!Inside(resolved)) throw new CliError(ErrorCode.BadArguments, $"{name}: {value} resolves to {resolved}, outside the allowed roots", "path_outside_roots");
                if (isOutput && name != "cache" && File.Exists(resolved) && (outputDir is null || !resolved.StartsWith(outputDir, StringComparison.Ordinal)))
                {
                    throw new CliError(ErrorCode.BadArguments, $"{name}: {value} exists and is outside the outbox; nothing outside outbox/ is written over", "path_outside_roots");
                }
            }
        }
        // positional paths (batch-xic PATHS…, info FILE) are inputs too
        foreach (var p in args.Positional)
        {
            var resolved = ResolveLinks(Path.GetFullPath(p));
            if (!Inside(resolved)) throw new CliError(ErrorCode.BadArguments, $"{p} resolves to {resolved}, outside the allowed roots", "path_outside_roots");
        }
        // the MS-DIAL workflow writes a folder of files: forced under an allowed root (checked above), never into the inbox itself
        if (Cli.MsdialCommands.Contains(command) && args.Get("o") is null && args.Get("output") is null && outputDir is not null) args = args.With("o", Path.Combine(outputDir, "msdial"));
        return args;
    }

    private bool Inside(string resolved)
    {
        foreach (var root in _roots.Concat(_inboxRoot is null ? Array.Empty<string>() : new[] { _inboxRoot }))
        {
            var r = ResolveLinks(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if ((resolved + Path.DirectorySeparatorChar).StartsWith(r, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>The real location of a path: every symbolic link along the existing part followed.</summary>
    internal static string ResolveLinks(string full)
    {
        var existing = full;
        var tail = new Stack<string>();
        while (!File.Exists(existing) && !Directory.Exists(existing))
        {
            var parent = Path.GetDirectoryName(existing);
            if (string.IsNullOrEmpty(parent) || parent == existing) return full;
            tail.Push(Path.GetFileName(existing));
            existing = parent;
        }
        try
        {
            var info = Directory.Exists(existing) ? new DirectoryInfo(existing) : new FileInfo(existing) as FileSystemInfo;
            var target = info.ResolveLinkTarget(true);
            if (target is not null) existing = target.FullName;
            // a parent may itself be a link
            var parentDir = Path.GetDirectoryName(existing);
            if (!string.IsNullOrEmpty(parentDir) && parentDir != existing)
            {
                var resolvedParent = ResolveLinks(parentDir);
                existing = Path.Combine(resolvedParent, Path.GetFileName(existing));
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        while (tail.Count > 0) existing = Path.Combine(existing, tail.Pop());
        return existing;
    }

    public static void WriteAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}

/// <summary>The file-queue server of <c>serve --inbox</c>.</summary>
internal sealed class InboxServer
{
    private readonly string _root;
    private readonly JobRunner _runner;
    private readonly Stopwatch _started = Stopwatch.StartNew();
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly SemaphoreSlim _wake = new(0);
    private int _running;
    private long _done;
    private string? _lastError;
    private (string Id, string Command, DateTime Since)? _current;

    public InboxServer(string root, List<string> allowRoots, RawStore store)
    {
        _root = root;
        AllowRoots = allowRoots;
        Directory.CreateDirectory(Inbox);
        Directory.CreateDirectory(Running);
        Directory.CreateDirectory(Outbox);
        Directory.CreateDirectory(Failed);
        _runner = new JobRunner(store, allowRoots, root, TimeSpan.FromSeconds(1800));
    }

    public List<string> AllowRoots { get; }
    public string Inbox => Path.Combine(_root, "inbox");
    public string Running => Path.Combine(_root, "running");
    public string Outbox => Path.Combine(_root, "outbox");
    public string Failed => Path.Combine(_root, "failed");
    public string StatusPath => Path.Combine(_root, "STATUS.json");
    public string LogPath => Path.Combine(_root, "log.jsonl");
    public string StopPath => Path.Combine(_root, "STOP");
    public int MaxJobs { get; set; } = 1;
    public TimeSpan? IdleExit { get; set; }
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(1800);
    public Action<string> Log { get; set; } = _ => { };
    public bool AllowMsdial { get => _runner.AllowMsdial; set => _runner.AllowMsdial = value; }
    internal bool AllowSleepForTests { get => _runner.AllowSleepForTests; set => _runner.AllowSleepForTests = value; }
    public TimeSpan Poll { get; set; } = TimeSpan.FromSeconds(1);

    public async Task RunAsync(CancellationToken ct)
    {
        RecoverStale();
        Log($"[serve] {Document.Tool} {Cli.Version} watching {Inbox}; roots: {(AllowRoots.Count == 0 ? "(the inbox only)" : string.Join(", ", AllowRoots))}; create {StopPath} to stop");
        WriteStatus();
        using var watcher = new FileSystemWatcher(Inbox) { Filter = "*.json", EnableRaisingEvents = true, IncludeSubdirectories = false };
        FileSystemEventHandler nudge = (_, _) => { try { _wake.Release(); } catch { } };
        watcher.Created += nudge;
        watcher.Renamed += (_, _) => { try { _wake.Release(); } catch { } };
        var idleSince = DateTime.UtcNow;
        var gate = new SemaphoreSlim(MaxJobs);
        var jobs = new List<Task>();
        while (!ct.IsCancellationRequested)
        {
            if (File.Exists(StopPath))
            {
                Log("[serve] STOP found; finishing the current job and leaving");
                break;
            }
            var next = Directory.EnumerateFiles(Inbox, "*.json").Where(f => !f.EndsWith(".tmp", StringComparison.Ordinal)).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).FirstOrDefault();
            if (next is null)
            {
                jobs.RemoveAll(j => j.IsCompleted);
                if (IdleExit is { } idle && jobs.Count == 0 && DateTime.UtcNow - idleSince > idle)
                {
                    Log($"[serve] nothing to do for {idle.TotalMinutes:F0} min; leaving");
                    break;
                }
                try { await _wake.WaitAsync(Poll, ct); } catch (OperationCanceledException) { break; }
                continue;
            }
            idleSince = DateTime.UtcNow;
            var id = Path.GetFileNameWithoutExtension(next);
            var running = Path.Combine(Running, Path.GetFileName(next));
            try { File.Move(next, running, overwrite: false); }
            catch (IOException) { continue; }   // another process claimed it, or the writer is still renaming
            await gate.WaitAsync(ct);
            jobs.Add(Task.Run(async () =>
            {
                try { await ExecuteAsync(id, running, ct); }
                finally { gate.Release(); }
            }, ct));
            jobs.RemoveAll(j => j.IsCompleted);
        }
        try { await Task.WhenAll(jobs); } catch { }
        try { File.Delete(StopPath); } catch { }
        WriteStatus(stopped: true);
        Log("[serve] stopped");
    }

    private async Task ExecuteAsync(string id, string requestPath, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var text = await File.ReadAllTextAsync(requestPath, ct);
        var command = TryCommand(text);
        Interlocked.Increment(ref _running);
        _current = (id, command, started);
        WriteStatus();
        var watch = Stopwatch.StartNew();
        var before = PeakRss();
        var (ok, answer, files) = await _runner.ExecuteAsync(text, Path.Combine(Outbox, id), ct);
        watch.Stop();
        var targetDir = ok ? Outbox : Failed;
        var answerPath = Path.Combine(targetDir, id + ".json");
        JobRunner.WriteAtomic(answerPath, answer);
        var kept = Path.Combine(targetDir, id + ".request.json");
        try { File.Move(requestPath, kept, overwrite: true); } catch (IOException) { }
        Interlocked.Decrement(ref _running);
        Interlocked.Increment(ref _done);
        _current = null;
        if (!ok) _lastError = $"{id}: {TryError(answer)}";
        AppendLog(new JsonObject
        {
            ["id"] = id, ["command"] = command, ["args"] = TryArgs(text), ["start"] = started.ToString("o"), ["end"] = DateTime.UtcNow.ToString("o"),
            ["seconds"] = Math.Round(watch.Elapsed.TotalSeconds, 3), ["exit"] = ok ? 0 : 1, ["ok"] = ok,
            ["answer"] = answerPath, ["outputs"] = new JsonArray(files.Select(f => (JsonNode)f).ToArray()),
            ["peak_rss_mb"] = Math.Round(Math.Max(before, PeakRss()) / 1048576.0, 1),
        });
        WriteStatus();
        Log($"[serve] {id} {command}: {(ok ? "ok" : "failed")} in {watch.Elapsed.TotalSeconds:F2} s");
    }

    /// <summary>The process's peak resident size; macOS reports no peak, so the current working set stands in.</summary>
    private static long PeakRss()
    {
        var p = Process.GetCurrentProcess();
        var peak = p.PeakWorkingSet64;
        return peak > 0 ? Math.Max(peak, p.WorkingSet64) : Math.Max(p.WorkingSet64, Environment.WorkingSet);
    }

    private void RecoverStale()
    {
        foreach (var stale in Directory.EnumerateFiles(Running, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(stale);
            JobRunner.WriteAtomic(Path.Combine(Failed, id + ".json"), Cli.ErrorDocument("interrupted", "the server stopped before this job finished"));
            try { File.Move(stale, Path.Combine(Failed, id + ".request.json"), overwrite: true); } catch (IOException) { }
            AppendLog(new JsonObject { ["id"] = id, ["command"] = "?", ["start"] = null, ["end"] = DateTime.UtcNow.ToString("o"), ["exit"] = 1, ["ok"] = false, ["error"] = "interrupted" });
            Log($"[serve] {id}: found in running/ at start, moved to failed/ as interrupted");
        }
    }

    private void WriteStatus(bool stopped = false)
    {
        var status = new JsonObject
        {
            ["schema"] = Document.Schema, ["tool"] = Document.Tool, ["version"] = Cli.Version, ["pid"] = Environment.ProcessId,
            ["started"] = _startedAt.ToString("o"), ["uptime_s"] = Math.Round(_started.Elapsed.TotalSeconds, 1), ["state"] = stopped ? "stopped" : "running",
            ["inbox"] = _root, ["allow_roots"] = new JsonArray(AllowRoots.Select(r => (JsonNode)r).ToArray()), ["max_jobs"] = MaxJobs,
            ["current"] = _current is { } c ? new JsonObject { ["id"] = c.Id, ["command"] = c.Command, ["elapsed_s"] = Math.Round((DateTime.UtcNow - c.Since).TotalSeconds, 1) } : null,
            ["running"] = _running, ["done"] = _done,
            ["queue"] = Directory.Exists(Inbox) ? Directory.EnumerateFiles(Inbox, "*.json").Count() : 0,
            ["last_error"] = _lastError, ["updated"] = DateTime.UtcNow.ToString("o"),
        };
        try { JobRunner.WriteAtomic(StatusPath, status.ToJsonString(Document.Pretty) + "\n"); } catch (IOException) { }
    }

    private void AppendLog(JsonObject line)
    {
        lock (this)
        {
            try { File.AppendAllText(LogPath, line.ToJsonString(Document.Compact) + "\n", new UTF8Encoding(false)); } catch (IOException) { }
        }
    }

    private static string TryCommand(string text)
    {
        try { return JsonNode.Parse(text)?["command"]?.GetValue<string>() ?? (JsonNode.Parse(text)?["batch"] is not null ? "batch" : "?"); } catch { return "?"; }
    }

    private static JsonNode? TryArgs(string text)
    {
        try { return JsonNode.Parse(text)?["args"]?.DeepClone(); } catch { return null; }
    }

    private static string TryError(string answer)
    {
        try { var n = JsonNode.Parse(answer); return $"{n?["error"]}: {n?["message"]}"; } catch { return answer.Trim(); }
    }
}
