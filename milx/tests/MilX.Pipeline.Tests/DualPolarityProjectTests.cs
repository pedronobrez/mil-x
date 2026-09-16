using MilX.Pipeline.Model;
using MilX.Pipeline.Project;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>
/// A batch that was acquired both ways. The polarity belongs to the injection, not to the method,
/// and it has to survive the project file — a run that forgets which half is which produces two
/// alignments that cannot be paired.
/// </summary>
public class DualPolarityProjectTests
{
    [Fact]
    public async Task The_polarity_of_every_injection_survives_the_project_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), "milx-dual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var project = new MilXProject
            {
                Name = "liver",
                OutputFolder = Path.Combine(folder, "results"),
                Samples =
                {
                    new ProjectSample { Path = Path.Combine(folder, "liver_01_pos.wiff"), Name = "liver 01", Polarity = IonPolarity.Positive },
                    new ProjectSample { Path = Path.Combine(folder, "liver_01_neg.wiff"), Name = "liver 01", Polarity = IonPolarity.Negative },
                },
            };
            var path = Path.Combine(folder, "liver.odproj");
            await project.SaveAsync(path);

            var read = await MilXProject.LoadAsync(path);
            Assert.Equal(IonPolarity.Positive, read.Samples[0].Polarity);
            Assert.Equal(IonPolarity.Negative, read.Samples[1].Polarity);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task A_project_written_before_polarities_existed_still_opens()
    {
        var folder = Path.Combine(Path.GetTempPath(), "milx-dual-old-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "old.odproj");
            await File.WriteAllTextAsync(path, """
            {
              "Version": 1,
              "Name": "old",
              "Mode": "LCMS",
              "OutputFolder": "results",
              "MethodText": "",
              "Samples": [ { "Path": "liver_01.wiff", "Name": "liver 01", "Class": "1" } ]
            }
            """);
            var read = await MilXProject.LoadAsync(path);
            Assert.Single(read.Samples);
            Assert.Equal(IonPolarity.Positive, read.Samples[0].Polarity);   // the default, not a crash
        }
        finally { Directory.Delete(folder, true); }
    }
}
