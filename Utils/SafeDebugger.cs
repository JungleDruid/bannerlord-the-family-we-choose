using System.Diagnostics;

namespace TheFamilyWeChoose.Utils;

public static class SafeDebugger
{
    [Conditional("DEBUG")]
    public static void Break() => Debugger.Break();
}