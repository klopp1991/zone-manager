using System.Windows;
using System.Windows.Controls;
using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Der Dialog «Fenster zuordnen» ersetzt die frühere Prozessauswahl: links die laufenden Fenster, rechts
/// die Zonen des Ziels. Übernommen wird nur der Dateiname.
/// </summary>
public sealed class AssignWindowDialogTests
{
    private static readonly RunningProcessEntry[] Sample =
    [
        new("Explorer.exe", @"C:\Windows\Explorer.exe", "Dokumente"),
        new("Teams.exe", @"C:\Apps\Teams.exe", "Chat")
    ];

    [Fact]
    public void Dialog_lists_every_supplied_process_and_starts_without_a_confirmation()
    {
        WpfThemeHost.Invoke(() =>
        {
            var dialog = new AssignWindowDialog(new MainViewModel(ConfigurationSamples.TwoLayouts(), []), AssignWindowDialog.Mode.Assign, Sample);
            var list = Assert.IsType<ListBox>(dialog.FindName("ProcessList"));
            var confirm = Assert.IsType<Button>(dialog.FindName("ConfirmButton"));
            var layouts = Assert.IsType<ComboBox>(dialog.FindName("LayoutSelector"));

            Assert.Equal(2, list.Items.Count);
            Assert.False(confirm.IsEnabled);
            Assert.Null(dialog.SelectedProcessName);
            Assert.Equal("Arbeit", Assert.IsType<MonitorLayout>(layouts.SelectedItem).Name);
            Assert.Equal(AppRuleEvent.WindowCreated, dialog.SelectedEvent);
        });
    }

    [Fact]
    public void Dialog_narrows_the_list_while_typing_and_needs_process_and_zone_before_confirming()
    {
        WpfThemeHost.Invoke(() =>
        {
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
            var dialog = new AssignWindowDialog(viewModel, AssignWindowDialog.Mode.Assign, Sample) { Left = -10000 };
            dialog.Show();
            try
            {
                var list = Assert.IsType<ListBox>(dialog.FindName("ProcessList"));
                var search = Assert.IsType<TextBox>(dialog.FindName("SearchText"));
                var confirm = Assert.IsType<Button>(dialog.FindName("ConfirmButton"));

                search.Text = "teams";
                Assert.Equal("Teams.exe", Assert.IsType<RunningProcessEntry>(Assert.Single(list.Items)).DisplayName);

                list.SelectedIndex = 0;
                Assert.Equal("Teams.exe", dialog.SelectedProcessName);
                Assert.False(confirm.IsEnabled);

                // Die Zonen des Ziellayouts stehen als Ablageflaechen bereit; ein Klick waehlt eine.
                dialog.UpdateLayout();
                var host = Assert.IsType<Grid>(dialog.FindName("ZoneHost"));
                var zones = host.Children.OfType<Border>().ToArray();
                Assert.Equal(2, zones.Length);
                zones[1].RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonUpEvent
                });

                Assert.True(confirm.IsEnabled);
                Assert.Equal(viewModel.Configuration.Layouts[0].Zones[1].Id, dialog.SelectedZoneId);
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    [Fact]
    public void In_exclusion_mode_the_target_side_is_hidden_and_a_process_alone_suffices()
    {
        WpfThemeHost.Invoke(() =>
        {
            var dialog = new AssignWindowDialog(new MainViewModel(ConfigurationSamples.TwoLayouts(), []), AssignWindowDialog.Mode.Exclude, Sample);
            var list = Assert.IsType<ListBox>(dialog.FindName("ProcessList"));
            var confirm = Assert.IsType<Button>(dialog.FindName("ConfirmButton"));

            Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(dialog.FindName("TargetPanel")).Visibility);
            Assert.Equal("In Ruhe lassen", confirm.Content);

            list.SelectedIndex = 0;

            Assert.True(confirm.IsEnabled);
            Assert.Equal("Explorer.exe", dialog.SelectedProcessName);
        });
    }

    [Fact]
    public void The_fullscreen_editor_shares_the_editor_of_the_main_window()
    {
        WpfThemeHost.Invoke(() =>
        {
            var identity = new MonitorIdentity("MONITOR", "DISPLAY1", "Testmonitor");
            var monitor = new LiveMonitor(identity, new MonitorWorkArea(0, 0, 3200, 1080), 96, 96, true);
            var window = new MainWindow();
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts() with
            {
                Layouts = [new MonitorLayout(identity, 3200, 1080, [new ZoneDefinition(Guid.NewGuid(), "Links", new NormalizedRect(0, 0, 0.5, 1))]) { Name = "Arbeiten" }]
            }, [monitor]);
            window.AttachViewModel(viewModel);

            var editor = new FullscreenZoneEditorWindow(window, viewModel);
            editor.RefreshFromEditor();
            var canvas = Assert.IsType<SnapZones.App.Controls.LayoutCanvas>(editor.FindName("Canvas"));

            Assert.Equal(SnapZones.App.Controls.CanvasPresentation.Fullscreen, canvas.Presentation);
            Assert.Single(canvas.Zones);
            Assert.Equal(3200, canvas.MonitorPixelWidth);
            Assert.Equal("Monitor 1 · Arbeiten", Assert.IsType<TextBlock>(editor.FindName("TitleText")).Text);

            // Undo und Redo laufen auf demselben Verlauf wie im Fenster.
            Assert.True(viewModel.Editor!.AddZone());
            editor.RefreshFromEditor();
            Assert.Equal(2, canvas.Zones.Count);
            Assert.True(Assert.IsType<Button>(editor.FindName("UndoButton")).IsEnabled);
        });
    }
}
