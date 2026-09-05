using System.Windows;
using SnapZones.App.ViewModels;

namespace SnapZones.App.Views;

/// <summary>
/// Der Assistent fuer den Fensterhelfer in drei Schritten: Erklaerung mit Vor- und Nachteilen,
/// Voraussetzungen, Einrichten. Die Rueckfrage vor dem Eingriff in den Zertifikatspeicher bleibt eine
/// MessageBox: ein Zertifikat laesst sich nicht per Toast zuruecknehmen.
/// </summary>
public partial class HelperWizardWindow : Window
{
    private readonly MainViewModel viewModel;
    private int step = 1;

    public HelperWizardWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(MainViewModel.CanInstall) or nameof(MainViewModel.InstallationStatus))
            {
                RefreshInstallation();
            }
        };
        ShowStep(1);
    }

    /// <summary>Der sichtbare Schritt, 1 bis 3.</summary>
    public int Step => step;

    private void ShowStep(int wanted)
    {
        step = Math.Clamp(wanted, 1, 3);
        Step1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        StepText.Text = step switch
        {
            1 => "Schritt 1 von 3 · Was eingerichtet wird",
            2 => "Schritt 2 von 3 · Voraussetzungen prüfen",
            _ => "Schritt 3 von 3 · Zertifikat und Fensterhelfer einrichten"
        };
        BackButton.IsEnabled = step > 1;
        NextButton.Visibility = step < 3 ? Visibility.Visible : Visibility.Collapsed;
        RefreshInstallation();
    }

    private void RefreshInstallation()
    {
        var installed = viewModel.IsInstalled;
        InstalledMark.Text = installed ? "✓" : "○";
        InstalledText.Text = installed
            ? "Erfüllt: das Programm läuft aus «Programme»."
            : "Noch nicht erfüllt: Windows akzeptiert uiAccess nur aus einem geschützten Verzeichnis. Installiere das Programm zuerst; es startet danach von dort neu.";
        InstallButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Back_Click(object sender, RoutedEventArgs eventArgs) => ShowStep(step - 1);

    private void Next_Click(object sender, RoutedEventArgs eventArgs) => ShowStep(step + 1);

    private void Close_Click(object sender, RoutedEventArgs eventArgs) => Close();

    private void Install_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (System.Windows.MessageBox.Show(
                this,
                "Die Programmdatei wird nach «Programme» kopiert, im Startmenü verknüpft und in "
                    + "«Apps und Features» eingetragen. Das Programm startet danach von dort neu.",
                "Installieren",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information) == MessageBoxResult.OK)
        {
            viewModel.Install();
        }
    }

    private void CertificateAction_Click(object sender, RoutedEventArgs eventArgs)
    {
        // Ein Eingriff in den Zertifikatspeicher des Rechners wird nicht beilaeufig bestaetigt. Die
        // Rueckfrage nennt darum noch einmal, was passiert und was es bedeutet.
        var (question, title) = viewModel.IsCertificateInstalled
            ? ("Das Zertifikat wird aus allen Speichern entfernt. Der Fensterhelfer startet danach "
                + "nicht mehr; für Fenster von Programmen mit Administratorrechten fragt das Programm "
                + "dann wieder nach eigenen Rechten.",
               "Zertifikat entfernen")
            : ("Es wird ein eigenes Zertifikat auf diesem Rechner erzeugt und in die "
                + "Vertrauensspeicher von Windows eingetragen. Damit wird anschliessend der "
                + "Fensterhelfer unterschrieben.\n\n"
                + "Dein Rechner vertraut danach allem, was mit diesem Zertifikat unterschrieben "
                + "wurde. Der geheime Schlüssel bleibt auf dieser Maschine und lässt sich nicht "
                + "exportieren; das Zertifikat kann keine weiteren Zertifikate ausstellen.\n\n"
                + "Windows fragt gleich nach Administratorrechten. Fortfahren?",
               "Zertifikat einrichten");

        if (System.Windows.MessageBox.Show(this, question, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }

        viewModel.ToggleCertificate();
    }
}
