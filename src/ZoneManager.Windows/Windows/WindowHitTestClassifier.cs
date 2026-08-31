namespace ZoneManager.Windows.Windows;

public static class WindowHitTestClassifier
{
    public static bool IsMoveOperation(int hitTest) => hitTest switch
    {
        -2 or -1 => false,
        4 => false,
        >= 10 and <= 17 => false,
        _ => true
    };
}
