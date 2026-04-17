using System.Runtime.CompilerServices;
using VerifyTests;

namespace DocFlow.Documentation.Tests;

/// <summary>
/// Verify.Xunit configuration. Keeps snapshot files beside the tests and forbids running
/// tests from machines other than a developer workstation from auto-accepting diffs.
/// </summary>
internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.DontScrubDateTimes();
        VerifierSettings.UseStrictJson();
    }
}
