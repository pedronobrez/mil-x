// Adapted from MS-DIAL 5 (LGPL-3.0):
//   tests/MSDIAL5/MsdialCoreTestApp/Process/CommonProcess.cs (ParseLibraries)
// Original copyright (c) RIKEN and the MS-DIAL contributors.
// Changes for OpenDIAL: single MSP/text/LBM library each, warnings go to a callback.

using CompMs.Common.Components;
using CompMs.Common.DataObj.Database;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Parser;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Utility;

namespace OpenDIAL.Pipeline.Internal;

internal sealed class LoadedLibraries
{
    public IupacDatabase Iupac { get; init; } = null!;
    public MoleculeDataBase? Msp { get; init; }
    public MoleculeDataBase? Text { get; init; }
    public MoleculeDataBase? Lbm { get; init; }
    public List<MoleculeMsReference> IsotopeText { get; init; } = new();
    public List<MoleculeMsReference> CompoundsInTargetMode { get; init; } = new();
}

internal static class LibraryLoader
{
    public static LoadedLibraries Load(ParameterBase param, Action<string> log)
    {
        MoleculeDataBase? msp = null, text = null, lbm = null;
        var isotopeText = new List<MoleculeMsReference>();
        var targets = new List<MoleculeMsReference>();

        if (Exists(param.MspFilePath))
        {
            var list = LibraryHandler.ReadMsLibrary(param.MspFilePath, param, out var error);
            msp = new MoleculeDataBase(list, "MspDB", DataBaseSource.Msp, SourceType.MspDB, param.MspFilePath);
            if (!string.IsNullOrEmpty(error)) log(error);
            log($"MSP library loaded: {list.Count} records from {param.MspFilePath}");
        }
        else if (!string.IsNullOrWhiteSpace(param.MspFilePath))
        {
            log($"MSP file was not found: {param.MspFilePath}");
        }

        if (Exists(param.LbmFilePath))
        {
            var list = LibraryHandler.ReadMsLibrary(param.LbmFilePath, param, out var error);
            lbm = new MoleculeDataBase(list, "LbmDB", DataBaseSource.Lbm, SourceType.MspDB, param.LbmFilePath);
            if (!string.IsNullOrEmpty(error)) log(error);
            log($"LBM library loaded: {list.Count} records");
        }

        if (Exists(param.TextDBFilePath))
        {
            var list = LibraryHandler.ReadMsLibrary(param.TextDBFilePath, param, out var error);
            text = new MoleculeDataBase(list, "TextDB", DataBaseSource.Text, SourceType.TextDB, param.TextDBFilePath);
            if (!string.IsNullOrEmpty(error)) log(error);
            log($"Text library loaded: {list.Count} records");
        }

        if (Exists(param.IsotopeTextDBFilePath))
        {
            isotopeText = TextLibraryParser.TextLibraryReader(param.IsotopeTextDBFilePath, out var error);
            if (!string.IsNullOrEmpty(error)) log(error);
        }

        if (Exists(param.CompoundListInTargetModePath))
        {
            targets = TextLibraryParser.CompoundListInTargetModeReader(param.CompoundListInTargetModePath, out var error);
            if (!string.IsNullOrEmpty(error)) log(error);
        }

        return new LoadedLibraries
        {
            Iupac = IupacResourceParser.GetIUPACDatabase(),
            Msp = msp,
            Text = text,
            Lbm = lbm,
            IsotopeText = isotopeText,
            CompoundsInTargetMode = targets,
        };
    }

    private static bool Exists(string? path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);
}
