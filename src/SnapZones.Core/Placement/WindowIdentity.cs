namespace SnapZones.Core.Placement;

public enum WindowKind
{
    MainWindow,
    Dialog
}

public sealed record WindowIdentity(string ApplicationKey, string WindowClass, WindowKind Kind);
