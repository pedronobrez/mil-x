using Xunit;
using MilX.Pipeline.Results;

namespace MilX.Pipeline.Tests;

/// <summary>
/// A project that has been copied to another machine names its dataset by a path that no longer
/// exists. These cover the search that finds it anyway — the thing that decides whether a folder
/// handed round on a memory stick opens on anybody's machine or only on the one that wrote it.
/// </summary>
public sealed class ProjectRelocationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "milx-relocate-" + Guid.NewGuid().ToString("N"));

    private string Dir(params string[] parts)
    {
        var path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Touch(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, "not really a dataset");
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void Finds_the_dataset_beside_the_project_file()
    {
        var folder = Dir("run");
        var dataset = Touch(folder, "Project-1.mddata");

        Assert.Equal(dataset, ProjectOpener.FindDataset("Project-1.mddata", folder));
    }

    [Fact]
    public void Finds_the_dataset_one_level_up()
    {
        var batch = Dir("batch");
        var dataset = Touch(batch, "Project-1.mddata");
        var results = Dir("batch", "results");

        Assert.Equal(dataset, ProjectOpener.FindDataset("Project-1.mddata", results));
    }

    [Fact]
    public void Finds_the_dataset_in_the_folder_next_door()
    {
        // how this pipeline lays a run out: the project file in prepared/, the dataset in data/
        var data = Dir("demo", "data");
        var dataset = Touch(data, "Project-1.mddata");
        var prepared = Dir("demo", "prepared");

        Assert.Equal(dataset, ProjectOpener.FindDataset("Project-1.mddata", prepared));
    }

    [Fact]
    public void Takes_only_the_name_from_the_stored_path()
    {
        // the stored path is another machine's, and on Windows it is not even a path shape this
        // operating system understands; only its last segment is of any use
        var data = Dir("demo", "data");
        var dataset = Touch(data, "Project-1.mddata");
        var prepared = Dir("demo", "prepared");

        Assert.Equal(dataset, ProjectOpener.FindDataset(@"C:\Users\someone\Documents\demo\data\Project-1.mddata", prepared));
        Assert.Equal(dataset, ProjectOpener.FindDataset("/Users/someone/Documents/demo/data/Project-1.mddata", prepared));
    }

    [Fact]
    public void Says_nothing_when_the_dataset_is_nowhere_near()
    {
        var prepared = Dir("demo", "prepared");
        Dir("demo", "data");

        Assert.Null(ProjectOpener.FindDataset("Project-1.mddata", prepared));
    }

    [Fact]
    public void Does_not_wander_off_on_an_empty_question()
    {
        var folder = Dir("run");
        Assert.Null(ProjectOpener.FindDataset("", folder));
        Assert.Null(ProjectOpener.FindDataset("Project-1.mddata", ""));
    }
}
