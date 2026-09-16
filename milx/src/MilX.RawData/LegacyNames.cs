using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace MilX.RawData;

/// <summary>
/// The program was called OpenDIAL until 1.0, and every knob it took from the environment was an
/// <c>OPENDIAL_*</c> variable. Those names are still honoured for one version: a script that sets
/// <c>OPENDIAL_MSCONVERT</c> keeps working, because the value is copied to <c>MILX_MSCONVERT</c>
/// before anything reads it. The new name wins when both are set.
///
/// Runs once, when this assembly is first touched, and again on demand from an entry point that
/// reads the environment before it touches the raw-data layer.
/// </summary>
public static class LegacyNames
{
    private const string OldPrefix = "OPENDIAL_";
    private const string NewPrefix = "MILX_";
    private static bool _adopted;

    /// <summary>Copies every <c>OPENDIAL_*</c> variable to its <c>MILX_*</c> name when that one is unset.</summary>
    [ModuleInitializer]
    public static void AdoptEnvironment()
    {
        if (_adopted) return;
        _adopted = true;
        try
        {
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                var name = entry.Key as string;
                if (name == null || !name.StartsWith(OldPrefix, StringComparison.Ordinal)) continue;
                var renamed = NewPrefix + name.Substring(OldPrefix.Length);
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(renamed)))
                {
                    Environment.SetEnvironmentVariable(renamed, entry.Value as string);
                }
            }
        }
        catch
        {
            // a locked-down environment is not a reason to fail to start
        }
    }
}
