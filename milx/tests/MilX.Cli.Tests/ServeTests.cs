using System.Text.Json.Nodes;
using MilX.Cli.Commands;
using Xunit;

namespace MilX.Cli.Tests;

/// <summary>
/// The file-queue server, driven the way the agent drives it: request files written atomically into
/// inbox/, answers read from outbox/ and failed/, STATUS.json and log.jsonl checked, STOP to leave.
/// Four requests: one valid, one with a path outside the roots, one with a command the allow-list
/// refuses, one that times out.
/// </summary>
public class ServeTests
{
    private static async Task<T> WaitFor<T>(Func<T?> probe, TimeSpan timeout) where T : class
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (probe() is { } found) return found;
            await Task.Delay(50);
        }
        throw new TimeoutException("the server did not answer in time");
    }

    private static void WriteRequest(string inbox, string id, JsonObject request)
    {
        var tmp = Path.Combine(inbox, id + ".json.tmp");
        File.WriteAllText(tmp, request.ToJsonString());
        File.Move(tmp, Path.Combine(inbox, id + ".json"));
    }

    [Fact]
    public async Task The_inbox_answers_a_request_byte_for_byte_and_refuses_what_it_must()
    {
        var root = TestData.Scratch("inbox");
        var data = TestData.Scratch("data");
        var outsideFile = Path.Combine(TestData.Scratch("outside"), "A.mzML");
        File.Copy(TestData.SampleA01, Path.Combine(data, "A.mzML"));
        File.Copy(TestData.SampleA01, outsideFile);
        var cacheDir = TestData.Scratch("cache");
        var store = new RawStore(cacheDir);
        var server = new InboxServer(root, new List<string> { data }, store) { Poll = TimeSpan.FromMilliseconds(100), AllowSleepForTests = true };
        using var cts = new CancellationTokenSource();
        var running = server.RunAsync(cts.Token);
        try
        {
            Assert.True(Directory.Exists(server.Inbox) && Directory.Exists(server.Outbox) && Directory.Exists(server.Failed) && Directory.Exists(server.Running));
            await WaitFor(() => File.Exists(server.StatusPath) ? "yes" : null, TimeSpan.FromSeconds(5));

            // 1. a valid request: the answer is exactly what the command line prints for the same call
            WriteRequest(server.Inbox, "0001-xic", new JsonObject
            {
                ["schema"] = "mil-cli/1", ["id"] = "0001-xic", ["command"] = "xic",
                ["args"] = new JsonObject { ["file"] = Path.Combine(data, "A.mzML"), ["mz"] = "195.0877, 138.0662", ["tol"] = 0.01 }, ["format"] = "json",
            });
            var answerPath = Path.Combine(server.Outbox, "0001-xic.json");
            var answer = await WaitFor(() => File.Exists(answerPath) ? File.ReadAllText(answerPath) : null, TimeSpan.FromSeconds(30));
            var direct = await Cli.RunAsync("xic", Args.Parse(new[] { Path.Combine(data, "A.mzML"), "--mz", "195.0877, 138.0662", "--tol", "0.01" }), new RawStore(TestData.Scratch("c")), null, true, CancellationToken.None);
            Assert.Equal(direct.ToJson(), answer);
            Assert.True(File.Exists(Path.Combine(server.Outbox, "0001-xic.request.json")));
            Assert.False(File.Exists(Path.Combine(server.Inbox, "0001-xic.json")));

            // 2. a path outside the roots, through a symlink inside them
            var link = Path.Combine(data, "link.mzML");
            File.CreateSymbolicLink(link, outsideFile);
            WriteRequest(server.Inbox, "0002-outside", new JsonObject { ["schema"] = "mil-cli/1", ["id"] = "0002-outside", ["command"] = "info", ["args"] = new JsonObject { ["file"] = link } });
            var failed2 = Path.Combine(server.Failed, "0002-outside.json");
            var outside = await WaitFor(() => File.Exists(failed2) ? JsonNode.Parse(File.ReadAllText(failed2)) : null, TimeSpan.FromSeconds(10));
            Assert.Equal("path_outside_roots", outside["error"]!.GetValue<string>());
            Assert.True(File.Exists(Path.Combine(server.Failed, "0002-outside.request.json")));

            // 3. a command the allow-list refuses: no shell, no eval, nothing but the contract
            WriteRequest(server.Inbox, "0003-shell", new JsonObject { ["schema"] = "mil-cli/1", ["id"] = "0003-shell", ["command"] = "sh", ["args"] = new JsonObject { ["c"] = "rm -rf /" } });
            var failed3 = Path.Combine(server.Failed, "0003-shell.json");
            var shell = await WaitFor(() => File.Exists(failed3) ? JsonNode.Parse(File.ReadAllText(failed3)) : null, TimeSpan.FromSeconds(10));
            Assert.Equal("not_allowed", shell["error"]!.GetValue<string>());

            // 4. a job that outlives its timeout
            WriteRequest(server.Inbox, "0004-slow", new JsonObject { ["schema"] = "mil-cli/1", ["id"] = "0004-slow", ["command"] = "_sleep", ["args"] = new JsonObject { ["seconds"] = 30 }, ["timeout_s"] = 1 });
            var failed4 = Path.Combine(server.Failed, "0004-slow.json");
            var slow = await WaitFor(() => File.Exists(failed4) ? JsonNode.Parse(File.ReadAllText(failed4)) : null, TimeSpan.FromSeconds(15));
            Assert.Equal("timeout", slow["error"]!.GetValue<string>());

            // 5. a batch: several requests in one file, answered in order
            WriteRequest(server.Inbox, "0005-batch", new JsonObject
            {
                ["schema"] = "mil-cli/1", ["id"] = "0005-batch",
                ["batch"] = new JsonArray(
                    new JsonObject { ["command"] = "info", ["args"] = new JsonObject { ["file"] = Path.Combine(data, "A.mzML") } },
                    new JsonObject { ["command"] = "scans", ["args"] = new JsonObject { ["file"] = Path.Combine(data, "A.mzML"), ["source"] = "ms2-all", ["count"] = true } }),
            });
            var batchPath = Path.Combine(server.Outbox, "0005-batch.json");
            var batch = await WaitFor(() => File.Exists(batchPath) ? JsonNode.Parse(File.ReadAllText(batchPath)) : null, TimeSpan.FromSeconds(30));
            Assert.Equal(2, batch["answers"]!.AsArray().Count);
            Assert.Equal("scans", batch["answers"]![1]!["command"]!.GetValue<string>());
            Assert.Equal(62, batch["answers"]![1]!["count"]!.GetValue<int>());

            // 6. a trace goes to a file under outbox/<id>/, and the answer lists it
            WriteRequest(server.Inbox, "0006-trace", new JsonObject { ["schema"] = "mil-cli/1", ["id"] = "0006-trace", ["command"] = "xic", ["args"] = new JsonObject { ["file"] = Path.Combine(data, "A.mzML"), ["mz"] = "195.0877", ["trace"] = true } });
            var tracePath = Path.Combine(server.Outbox, "0006-trace.json");
            var traceAnswer = await WaitFor(() => File.Exists(tracePath) ? JsonNode.Parse(File.ReadAllText(tracePath)) : null, TimeSpan.FromSeconds(30));
            var listed = traceAnswer["files"]!.AsArray()[0]!.GetValue<string>();
            Assert.StartsWith(Path.Combine(server.Outbox, "0006-trace"), listed);
            Assert.True(File.Exists(listed));
            Assert.Null(traceAnswer["xics"]![0]!["trace"]);

            // the status and the log
            var status = JsonNode.Parse(File.ReadAllText(server.StatusPath))!;
            Assert.Equal("milx", status["tool"]!.GetValue<string>());
            Assert.Equal(Environment.ProcessId, status["pid"]!.GetValue<int>());
            Assert.Equal(6, status["done"]!.GetValue<long>());
            Assert.Contains("0004-slow", status["last_error"]!.GetValue<string>());
            var log = File.ReadAllLines(server.LogPath).Select(l => JsonNode.Parse(l)!).ToList();
            Assert.Equal(6, log.Count);
            Assert.Equal("xic", log[0]["command"]!.GetValue<string>());
            Assert.True(log[0]["ok"]!.GetValue<bool>());
            Assert.False(log[1]["ok"]!.GetValue<bool>());
            Assert.True(log[0]["peak_rss_mb"]!.GetValue<double>() > 0);
            Assert.All(log, l => Assert.DoesNotContain("binaryDataArray", l.ToJsonString()));   // the log never carries file contents

            // STOP finishes and leaves, removing the sentinel
            File.WriteAllText(server.StopPath, string.Empty);
            await running.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(File.Exists(server.StopPath));
            Assert.Equal("stopped", JsonNode.Parse(File.ReadAllText(server.StatusPath))!["state"]!.GetValue<string>());
        }
        finally
        {
            cts.Cancel();
            try { await running; } catch { }
        }
    }

    [Fact]
    public async Task A_stale_job_in_running_is_failed_as_interrupted_at_start()
    {
        var root = TestData.Scratch("inbox-stale");
        Directory.CreateDirectory(Path.Combine(root, "running"));
        File.WriteAllText(Path.Combine(root, "running", "0009-old.json"), "{\"command\":\"info\",\"args\":{}}");
        var server = new InboxServer(root, new List<string>(), new RawStore(TestData.Scratch("cache"))) { Poll = TimeSpan.FromMilliseconds(50), IdleExit = TimeSpan.FromMilliseconds(200) };
        await server.RunAsync(CancellationToken.None);
        var failed = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "failed", "0009-old.json")))!;
        Assert.Equal("interrupted", failed["error"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(root, "failed", "0009-old.request.json")));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "running")));
    }

    [Fact]
    public async Task Outputs_never_write_over_a_file_outside_the_outbox()
    {
        var data = TestData.Scratch("data2");
        File.Copy(TestData.SampleA01, Path.Combine(data, "A.mzML"));
        var existing = Path.Combine(data, "keep.mzML");
        File.WriteAllText(existing, "precious");
        var runner = new JobRunner(new RawStore(TestData.Scratch("cache")), new List<string> { data }, TestData.Scratch("inbox2"), TimeSpan.FromSeconds(60));
        var (ok, text, _) = await runner.ExecuteAsync(new JsonObject { ["command"] = "convert", ["args"] = new JsonObject { ["file"] = Path.Combine(data, "A.mzML"), ["out"] = existing } }.ToJsonString(), null, CancellationToken.None);
        Assert.False(ok);
        Assert.Equal("path_outside_roots", JsonNode.Parse(text)!["error"]!.GetValue<string>());
        Assert.Equal("precious", File.ReadAllText(existing));
        // a new file under a root is fine
        var fresh = Path.Combine(data, "fresh.mzML");
        var (ok2, _, files) = await runner.ExecuteAsync(new JsonObject { ["command"] = "convert", ["args"] = new JsonObject { ["file"] = Path.Combine(data, "A.mzML"), ["out"] = fresh } }.ToJsonString(), null, CancellationToken.None);
        Assert.True(ok2);
        Assert.Contains(fresh, files);
    }
}
