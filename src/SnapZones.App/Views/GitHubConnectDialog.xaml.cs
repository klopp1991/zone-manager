using System.Diagnostics;
using System.Windows;
using SnapZones.App.Services;
using SnapZones.Core.Updates;

namespace SnapZones.App.Views;

/// <summary>
/// Der Gerätecode-Dialog: er zeigt den Code, öffnet die Bestätigungsseite und wartet, bis GitHub die
/// Anmeldung bestätigt. Der Zugangsschlüssel selbst erscheint nie im Fenster.
/// </summary>
public partial class GitHubConnectDialog : Window
{
    private readonly GitHubAuthService auth;
    private readonly CancellationTokenSource cancellation = new();
    private GitHubDeviceCode? code;

    public GitHubConnectDialog(GitHubAuthService auth)
    {
        this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
        InitializeComponent();
        Loaded += async (_, _) => await StartAsync();
        Closed += (_, _) =>
        {
            cancellation.Cancel();
            cancellation.Dispose();
        };
    }

    /// <summary>Das Ergebnis, sobald der Dialog geschlossen ist.</summary>
    public GitHubConnectResult? Result { get; private set; }

    private async Task StartAsync()
    {
        // Ohne hinterlegte OAuth-Anwendung gibt es keinen Geraetecode; dann fuehrt nur das Token hin.
        if (!GitHubAuthService.HasDeviceFlow)
        {
            DeviceSteps.Visibility = Visibility.Collapsed;
            IntroText.Text = "Die neuen Fassungen liegen in einem privaten Bereich. Damit Zone Manager "
                + "sie herunterladen darf, füge hier ein Zugriffstoken deines GitHub-Kontos ein.";
            TokenTitleText.Text = "Zugriffstoken";
            TokenText.Focus();
            return;
        }

        RetryButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Fordere einen Code an …";
        UserCodeText.Text = "————";
        VerificationUriText.Text = "github.com/login/device";
        code = await auth.RequestDeviceCodeAsync(cancellation.Token);
        if (code is null)
        {
            StatusText.Text = "GitHub ist gerade nicht erreichbar. Versuch es später noch einmal.";
            RetryButton.Visibility = Visibility.Visible;
            return;
        }

        UserCodeText.Text = code.UserCode;
        VerificationUriText.Text = code.VerificationUri
            .Replace("https://", string.Empty, StringComparison.Ordinal)
            .TrimEnd('/');
        StatusText.Text = "Warte auf die Bestätigung …";

        var progress = new Progress<GitHubConnectProgress>(update =>
            StatusText.Text = $"{update.Message} Code gültig noch {GitHubDeviceFlow.DescribeRemaining(update.Remaining)}");
        var result = await auth.AwaitApprovalAsync(code, progress, cancellation.Token);
        Finish(result);
    }

    private void Finish(GitHubConnectResult result)
    {
        Result = result;
        StatusText.Text = result.Message;
        TokenStatusText.Text = result.Message;
        if (result.Connected)
        {
            DialogResult = true;
            Close();
            return;
        }

        RetryButton.Visibility = GitHubAuthService.HasDeviceFlow ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CopyCode_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (code is null)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(code.UserCode);
            StatusText.Text = "Der Code liegt in der Zwischenablage.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            StatusText.Text = "Die Zwischenablage war belegt. Tipp den Code von Hand ab.";
        }
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (code is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(code.VerificationUri) { UseShellExecute = true });
            StatusText.Text = "Warte auf die Bestätigung …";
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            StatusText.Text = $"Die Seite liess sich nicht öffnen: {exception.Message}";
        }
    }

    private async void UseToken_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        UseTokenButton.IsEnabled = false;
        try
        {
            Finish(await auth.ConnectWithTokenAsync(TokenText.Text, cancellation.Token));
        }
        finally
        {
            UseTokenButton.IsEnabled = true;
        }
    }

    private async void Retry_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        await StartAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Close();
    }
}
