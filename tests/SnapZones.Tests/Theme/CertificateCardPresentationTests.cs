using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Die Karte zeigt den Zustand des Zertifikats und genau eine Schaltfläche: einrichten, solange es fehlt —
/// entfernen, sobald es steht. Zwei Schaltflächen, von denen immer eine grau ist, wären hier nur Ballast.
/// </summary>
public sealed class CertificateCardPresentationTests
{
    [Fact]
    public void Without_a_certificate_the_card_offers_to_set_one_up()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);

        Assert.False(viewModel.IsCertificateInstalled);
        Assert.Equal("Nicht eingerichtet", viewModel.CertificateStateLabel);
        Assert.Equal("Zertifikat einrichten", viewModel.CertificateActionLabel);
        Assert.Contains("erzeugt", viewModel.CertificateActionHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void With_a_certificate_the_same_button_offers_to_remove_it()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), [])
        {
            IsCertificateInstalled = true
        };

        Assert.Equal("Eingerichtet", viewModel.CertificateStateLabel);
        Assert.Equal("Zertifikat entfernen", viewModel.CertificateActionLabel);
        Assert.Contains("Fensterhelfer startet danach nicht mehr", viewModel.CertificateActionHint, StringComparison.Ordinal);
    }

    [Fact]
    public void Changing_the_state_announces_every_dependent_text()
    {
        // Ohne diese Meldungen bliebe die Beschriftung stehen, nachdem das Zertifikat eingerichtet wurde.
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        var announced = new List<string>();
        viewModel.PropertyChanged += (_, arguments) => announced.Add(arguments.PropertyName ?? string.Empty);

        viewModel.IsCertificateInstalled = true;

        Assert.Contains(nameof(MainViewModel.CertificateStateLabel), announced);
        Assert.Contains(nameof(MainViewModel.CertificateActionLabel), announced);
        Assert.Contains(nameof(MainViewModel.CertificateActionHint), announced);
    }

    [Fact]
    public void The_single_button_triggers_whichever_direction_the_state_calls_for()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        var installs = 0;
        var removals = 0;
        viewModel.CertificateInstallRequested += () => installs++;
        viewModel.CertificateRemoveRequested += () => removals++;

        viewModel.ToggleCertificate();
        Assert.Equal(1, installs);
        Assert.Equal(0, removals);

        viewModel.IsCertificateInstalled = true;
        viewModel.ToggleCertificate();
        Assert.Equal(1, installs);
        Assert.Equal(1, removals);
    }

    [Fact]
    public void The_program_page_shows_the_state_and_the_wizard_holds_the_single_button()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
            window.AttachViewModel(viewModel);

            var state = Assert.IsType<TextBlock>(window.FindName("CertificateStateText"));
            var certificate = Assert.IsType<TextBlock>(window.FindName("CertificateStatusText"));
            var helper = Assert.IsType<TextBlock>(window.FindName("HelperStatusText"));
            Assert.IsType<Button>(window.FindName("HelperWizardButton"));
            Assert.Null(window.FindName("InstallCertificateButton"));
            Assert.Null(window.FindName("RemoveCertificateButton"));
            Assert.Null(window.FindName("CertificateActionButton"));

            Assert.Equal("CertificateStateLabel", state.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal("CertificateStatus", certificate.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal("HelperStatus", helper.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);

            // Der Assistent fuehrt in drei Schritten zur einen Schaltflaeche, deren Beschriftung dem Zustand folgt.
            var wizard = new HelperWizardWindow(viewModel);
            Assert.Equal(1, wizard.Step);
            var action = Assert.IsType<Button>(wizard.FindName("CertificateActionButton"));
            Assert.Equal(
                "CertificateActionLabel",
                action.GetBindingExpression(ContentControl.ContentProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "CertificateActionLabel",
                action.GetBindingExpression(AutomationProperties.NameProperty)!.ParentBinding.Path.Path);
            var text = string.Join("\n", UiTree.LogicalDescendants<TextBlock>(wizard).Select(block => block.Text));
            Assert.Contains("Was du dafür in Kauf nimmst", text, StringComparison.Ordinal);
            Assert.Contains("Voraussetzungen", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Opening_the_settings_page_asks_for_a_fresh_state()
    {
        // Zertifikat und Helfer koennen sich ausserhalb des Programms aendern; ein einmal beim Start
        // gelesener Zustand waere dann falsch.
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));
            var refreshes = 0;
            window.SettingsPageOpened += () => refreshes++;

            var tabs = Assert.IsType<TabControl>(Assert.IsType<Grid>(window.Content).Children[1]);
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Programm"));

            Assert.Equal(1, refreshes);

            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Monitore"));
            Assert.Equal(1, refreshes);
        });
    }
}
