using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Windows;

internal static class WindowsIntegrityLevelReader
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;

    internal static bool CanControl(uint targetProcessId)
    {
        try
        {
            var ownIntegrityLevel = TryReadIntegrityLevel((uint)Environment.ProcessId);
            var targetIntegrityLevel = TryReadIntegrityLevel(targetProcessId);
            return ownIntegrityLevel.HasValue &&
                targetIntegrityLevel.HasValue &&
                ownIntegrityLevel.Value >= targetIntegrityLevel.Value;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static uint? TryReadIntegrityLevel(uint processId)
    {
        using var processHandle = Kernel32.OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle.IsInvalid || !Advapi32.OpenProcessToken(processHandle, TokenQuery, out var tokenHandle))
        {
            return null;
        }

        using (tokenHandle)
        {
        nint tokenInformation = 0;
        try
        {
            _ = Advapi32.GetTokenInformation(tokenHandle, TokenIntegrityLevel, 0, 0, out var requiredLength);
            if (requiredLength == 0)
            {
                return null;
            }

            tokenInformation = Marshal.AllocHGlobal(checked((int)requiredLength));
            if (!Advapi32.GetTokenInformation(
                    tokenHandle,
                    TokenIntegrityLevel,
                    tokenInformation,
                    requiredLength,
                    out _))
            {
                return null;
            }

            var mandatoryLabel = Marshal.PtrToStructure<TokenMandatoryLabelNative>(tokenInformation);
            if (mandatoryLabel.Label.Sid == 0)
            {
                return null;
            }

            var subAuthorityCountPointer = Advapi32.GetSidSubAuthorityCount(mandatoryLabel.Label.Sid);
            if (subAuthorityCountPointer == 0)
            {
                return null;
            }

            var subAuthorityCount = Marshal.ReadByte(subAuthorityCountPointer);
            if (subAuthorityCount == 0)
            {
                return null;
            }

            var integrityLevelPointer = Advapi32.GetSidSubAuthority(mandatoryLabel.Label.Sid, (uint)(subAuthorityCount - 1));
            return integrityLevelPointer == 0 ? null : unchecked((uint)Marshal.ReadInt32(integrityLevelPointer));
        }
        finally
        {
            if (tokenInformation != 0)
            {
                Marshal.FreeHGlobal(tokenInformation);
            }

        }
        }
    }
}
