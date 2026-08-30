using System.Windows;
using System.Windows.Threading;

namespace SnapZones.App.Services;

public partial class ProfileChangedToast : Window
{
    private readonly DispatcherTimer timer;

    public ProfileChangedToast()
    {
        InitializeComponent();
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Hide();
        };
    }

    public void ShowProfile(string profileName)
    {
        ProfileNameText.Text = profileName;
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 18;
        Top = workArea.Bottom - Height - 18;
        Show();
        timer.Stop();
        timer.Start();
    }
}
