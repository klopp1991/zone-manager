using System.Runtime.InteropServices;

namespace SnapZones.Windows.Native;

/// <summary>Zustand eines Geraeteknotens: laeuft er, oder meldet Windows ein Problem (Code 43 und Co.).</summary>
internal static class CfgMgr32
{
    internal const int CrSuccess = 0;
    internal const uint DnHasProblem = 0x00000400;

    [DllImport("cfgmgr32.dll")]
    internal static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);
}
