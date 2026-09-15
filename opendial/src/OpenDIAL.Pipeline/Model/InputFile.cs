using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Pipeline.Model;

/// <summary>One raw data file (or vendor directory) to process, plus its experimental metadata.</summary>
public sealed class InputFile
{
    public InputFile(string path)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Name = System.IO.Path.GetFileNameWithoutExtension(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    }

    /// <summary>Absolute path of the raw file (or the .d/.raw directory).</summary>
    public string Path { get; }

    /// <summary>Display name (file name without extension). Used as the analysis file name.</summary>
    public string Name { get; set; }

    /// <summary>Free-text class/group label (e.g. "Control", "Treated").</summary>
    public string Class { get; set; } = "1";

    public SampleType SampleType { get; set; } = SampleType.Sample;

    public AcquisitionMode Acquisition { get; set; } = AcquisitionMode.DDA;

    /// <summary>
    /// Which polarity this injection was acquired in. A batch is usually all one, and then this
    /// changes nothing; a batch with both is processed as two runs, because an alignment only
    /// means anything within one polarity.
    /// </summary>
    public IonPolarity Polarity { get; set; } = IonPolarity.Positive;

    /// <summary>1-based injection order.</summary>
    public int AnalyticalOrder { get; set; } = 1;

    public int Batch { get; set; } = 1;

    /// <summary>Whether the file is included in processing.</summary>
    public bool Included { get; set; } = true;

    /// <summary>True when the file is a vendor format that needs the msconvert bridge (a natively readable .wiff is not).</summary>
    public bool IsVendorFormat => FileFormats.IsVendorFormat(Path) && !WiffSupport.CanReadNatively(Path);
}
