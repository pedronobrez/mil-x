using System.Globalization;
using System.Runtime.CompilerServices;

namespace MilX.Pipeline.Tests;

/// <summary>MS-DIAL parses numbers with the current culture; force the invariant culture before any test runs.</summary>
internal static class CultureInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}
