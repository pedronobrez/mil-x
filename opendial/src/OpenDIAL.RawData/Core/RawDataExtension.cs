namespace CompMs.RawDataHandler.Core;

/// <summary>
/// Raw data formats known to the access layer. The member names and order match the
/// closed RawDataHandler package so that code compiled against either binary behaves
/// identically.
/// </summary>
public enum RawDataExtension
{
    abf,
    mzml,
    imzml,
    cdf,
    raw,
    d,
    ibf,
    wiff,
    wiff2,
    lcd,
    qgd,
    lrp,
}
