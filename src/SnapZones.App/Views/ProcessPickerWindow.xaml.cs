using System.Windows;
using SnapZones.App.Services;

namespace SnapZones.App.Views;

/// <summary>
/// Modale Auswahl eines laufenden Programms für App-Regeln.
/// Die Prozessliste wird beim Öffnen einmalig übergeben, damit das Fenster ohne echte Prozesse prüfbar bleibt.
/// </summary>
public partial class ProcessPickerWindow : Window
{
    private readonly IReadOnlyList<RunningProcessEntry> allProcesses;

    public ProcessPickerWindow()
        : this(RunningProcessCatalog.FromSystem())
    {
    }

    public ProcessPickerWindow(IReadOnlyList<RunningProcessEntry> processes)
    {
        allProcesses = processes ?? throw new ArgumentNullException(nameof(processes));
        InitializeComponent();
        ProcessList.ItemsSource = allProcesses;
    }

    /// <summary>Der übernommene Prozesspfad beziehungsweise Programmname; <c>null</c>, solange nichts bestätigt wurde.</summary>
    public string? SelectedProcessPath { get; private set; }

    private void Search_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        ProcessList.ItemsSource = RunningProcessCatalog.Filter(allProcesses, SearchText.Text);
        UpdateConfirmState();
    }

    private void ProcessList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        UpdateConfirmState();
    }

    private void ProcessList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Confirm();
    }

    private void Confirm_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Confirm();
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        DialogResult = false;
    }

    private void Confirm()
    {
        if (ProcessList.SelectedItem is not RunningProcessEntry entry)
        {
            return;
        }

        SelectedProcessPath = entry.ProcessPath;
        DialogResult = true;
    }

    private void UpdateConfirmState() =>
        ConfirmButton.IsEnabled = ProcessList.SelectedItem is RunningProcessEntry;
}
