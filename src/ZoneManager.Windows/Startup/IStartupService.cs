namespace ZoneManager.Windows.Startup;

public interface IStartupService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}
