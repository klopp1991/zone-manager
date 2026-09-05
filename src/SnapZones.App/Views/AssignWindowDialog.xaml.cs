using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.Core.AppRules;
using SnapZones.Core.Models;

namespace SnapZones.App.Views;

/// <summary>
/// Der Dialog «Fenster zuordnen»: links die laufenden Fenster, rechts der Zielmonitor mit seinen Zonen.
/// Ein Fenster wird auf eine Zone gezogen oder ein Fenster gewaehlt und eine Zone angeklickt. Uebernommen
/// wird nur der Dateiname des Programms, damit die Zuordnung ein Update ueberlebt. Im Modus «Exclude»
/// entfaellt der Zielteil; der Dialog dient dann der Seite «In Ruhe lassen» als Auswahl.
/// </summary>
public partial class AssignWindowDialog : Window
{
    public enum Mode
    {
        Assign,
        Exclude,
        PickProgram
    }

    private const string DragFormat = "ZoneManager.RunningProcess";
    private readonly MainViewModel viewModel;
    private readonly Mode mode;
    private readonly IReadOnlyList<RunningProcessEntry> allProcesses;
    private readonly Dictionary<Guid, Border> zoneElements = [];
    private System.Windows.Point dragStart;
    private Guid? hoveredZoneId;

    public AssignWindowDialog(MainViewModel viewModel, Mode mode)
        : this(viewModel, mode, RunningProcessCatalog.FromSystem())
    {
    }

    public AssignWindowDialog(MainViewModel viewModel, Mode mode, IReadOnlyList<RunningProcessEntry> processes)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.mode = mode;
        allProcesses = processes ?? throw new ArgumentNullException(nameof(processes));
        InitializeComponent();
        ProcessList.ItemsSource = allProcesses;
        EventSelector.ItemsSource = viewModel.AppRules.Events;
        EventSelector.SelectedItem = AppRuleEvent.WindowCreated;
        LayoutSelector.ItemsSource = viewModel.AppRules.TargetLayouts;
        LayoutSelector.SelectedItem = viewModel.AppRules.TargetLayouts.FirstOrDefault(layout => layout.IsActive && viewModel.SelectedMonitor is { } monitor &&
                Core.Layouts.LayoutService.BelongsToMonitor(layout.Monitor, monitor.Live.Identity))
            ?? viewModel.AppRules.TargetLayouts.FirstOrDefault();
        if (mode != Mode.Assign)
        {
            TargetColumn.Width = new GridLength(0);
            TargetContentColumn.Width = new GridLength(0);
            TargetPanel.Visibility = Visibility.Collapsed;
            Width = 420;
            Title = mode == Mode.Exclude ? "Programm in Ruhe lassen" : "Programm wählen";
            TitleText.Text = Title;
            HelpText.Text = mode == Mode.Exclude
                ? "Wähle das Programm, das Zone Manager nie anfassen soll. Übernommen wird nur der Dateiname, damit der Eintrag auch nach einem Update greift."
                : "Wähle ein laufendes Programm. Übernommen wird nur der Dateiname – etwa «Discord.exe» –, damit die Zuordnung auch nach einem Update greift.";
            ConfirmButton.Content = mode == Mode.Exclude ? "In Ruhe lassen" : "Übernehmen";
        }
    }

    /// <summary>Ein Chip «×» im Zielmonitor wurde angeklickt; der Aufrufer entfernt die Zuordnung mit Toast.</summary>
    public event Action<AppRule>? RemoveRuleRequested;

    /// <summary>Der uebernommene Programmname (nur der Dateiname); null, solange nichts bestaetigt wurde.</summary>
    public string? SelectedProcessName { get; private set; }

    public Guid? SelectedLayoutId { get; private set; }
    public Guid? SelectedZoneId { get; private set; }
    public AppRuleEvent SelectedEvent => EventSelector.SelectedItem as AppRuleEvent? ?? AppRuleEvent.WindowCreated;

    /// <summary>Wahr, wenn nach dem Anlegen gleich das Detail zum Eingrenzen geoeffnet werden soll.</summary>
    public bool OpenDetails { get; private set; }

    private MonitorLayout? TargetLayout => LayoutSelector.SelectedItem as MonitorLayout;

    private void Search_TextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        ProcessList.ItemsSource = RunningProcessCatalog.Filter(allProcesses, SearchText.Text);
        UpdateConfirmState();
    }

    private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (ProcessList.SelectedItem is RunningProcessEntry entry)
        {
            SelectedProcessName = entry.RuleIdentity;
        }

        UpdateConfirmState();
    }

    private void ProcessList_MouseDoubleClick(object sender, MouseButtonEventArgs eventArgs)
    {
        if (mode != Mode.Assign && ProcessList.SelectedItem is RunningProcessEntry)
        {
            Confirm();
        }
    }

    private void ProcessList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs) => dragStart = eventArgs.GetPosition(this);

    private void ProcessList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs eventArgs)
    {
        if (mode != Mode.Assign || eventArgs.LeftButton != MouseButtonState.Pressed || ProcessList.SelectedItem is not RunningProcessEntry entry)
        {
            return;
        }

        var position = eventArgs.GetPosition(this);
        if (Math.Abs(position.X - dragStart.X) < 6 && Math.Abs(position.Y - dragStart.Y) < 6)
        {
            return;
        }

        var data = new System.Windows.DataObject(DragFormat, entry.RuleIdentity);
        DragDrop.DoDragDrop(ProcessList, data, System.Windows.DragDropEffects.Link);
        HighlightZone(null);
    }

    private void LayoutSelector_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        SelectedZoneId = null;
        RenderZones();
        UpdateConfirmState();
    }

    private void ZoneHost_SizeChanged(object sender, SizeChangedEventArgs eventArgs) => RenderZones();

    private void RenderZones()
    {
        ZoneHost.Children.Clear();
        zoneElements.Clear();
        var layout = TargetLayout;
        if (layout is null)
        {
            return;
        }

        var ratio = layout.SavedHeight > 0 ? (double)layout.SavedWidth / layout.SavedHeight : 16d / 9d;
        var width = Math.Max(1, ZoneHost.ActualWidth);
        ZoneHost.Height = Math.Clamp(width / ratio, 160, 420);
        var height = ZoneHost.Height;
        var number = 0;
        foreach (var zone in layout.Zones)
        {
            number++;
            var rules = viewModel.AppRules.Rules.Where(rule => rule.TargetLayoutId == layout.Id && rule.TargetZoneId == zone.Id).ToArray();
            var chips = new WrapPanel { Margin = new Thickness(6, 4, 6, 0) };
            foreach (var rule in rules)
            {
                chips.Children.Add(BuildChip(rule));
            }

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = $"{number} · {zone.Name}",
                FontSize = 13,
                Margin = new Thickness(8, 6, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            content.Children.Add(chips);
            var dropHint = new TextBlock
            {
                Text = "Hier ablegen",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8),
                Visibility = Visibility.Collapsed,
                Tag = "DropHint"
            };
            dropHint.SetResourceReference(TextBlock.ForegroundProperty, "DropTargetInkBrush");
            var grid = new Grid();
            grid.Children.Add(content);
            grid.Children.Add(dropHint);
            var element = new Border
            {
                Child = grid,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(1),
                Width = Math.Max(1, zone.Bounds.Width * width - 2),
                Height = Math.Max(1, zone.Bounds.Height * height - 2),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Tag = zone.Id,
                Cursor = System.Windows.Input.Cursors.Hand,
                AllowDrop = true
            };
            element.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, $"Zone {number} {zone.Name} als Ziel wählen");
            element.RenderTransform = new TranslateTransform(zone.Bounds.X * width, zone.Bounds.Y * height);
            element.MouseLeftButtonUp += (_, _) => SelectZone(zone.Id);
            element.DragEnter += (_, _) => HighlightZone(zone.Id);
            zoneElements[zone.Id] = element;
            ZoneHost.Children.Add(element);
        }

        ApplyZoneStyles();
    }

    private Border BuildChip(AppRule rule)
    {
        var text = new TextBlock { Text = rule.DisplayName, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        var remove = new System.Windows.Controls.Button
        {
            Content = "×",
            MinHeight = 0,
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(4, 0, 0, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 12,
            ToolTip = "Zuordnung entfernen"
        };
        remove.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, $"Zuordnung {rule.DisplayName} entfernen");
        remove.Click += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            RemoveRuleRequested?.Invoke(rule);
            RenderZones();
        };
        var chip = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 2, 2),
            Margin = new Thickness(0, 0, 4, 4),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Children = { text, remove } }
        };
        chip.SetResourceReference(Border.BackgroundProperty, "SurfaceRaisedBrush");
        chip.SetResourceReference(Border.BorderBrushProperty, "ControlBorderBrush");
        return chip;
    }

    private void ApplyZoneStyles()
    {
        foreach (var (zoneId, element) in zoneElements)
        {
            var hovered = zoneId == hoveredZoneId;
            var selected = zoneId == SelectedZoneId;
            if (hovered)
            {
                element.SetResourceReference(Border.BorderBrushProperty, "DropTargetBrush");
                element.BorderThickness = new Thickness(2);
                element.Background = ResourceColourBrush("DropTargetBrush", 0.22);
            }
            else if (selected)
            {
                element.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                element.BorderThickness = new Thickness(2.5);
                element.Background = ResourceColourBrush("ZoneFillBrush", 0.32);
            }
            else
            {
                element.SetResourceReference(Border.BorderBrushProperty, "ZoneBorderBrush");
                element.BorderThickness = new Thickness(1.5);
                element.Background = ResourceColourBrush("ZoneFillBrush", 0.18);
            }

            if (element.Child is Grid grid)
            {
                foreach (var hint in grid.Children.OfType<TextBlock>().Where(block => Equals(block.Tag, "DropHint")))
                {
                    hint.Visibility = hovered ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    private System.Windows.Media.Brush ResourceColourBrush(string key, double opacity)
    {
        var colour = TryFindResource(key) is SolidColorBrush brush ? brush.Color : System.Windows.Media.Color.FromRgb(112, 112, 112);
        return new SolidColorBrush(colour) { Opacity = opacity };
    }

    private void HighlightZone(Guid? zoneId)
    {
        hoveredZoneId = zoneId;
        ApplyZoneStyles();
    }

    private void SelectZone(Guid zoneId)
    {
        SelectedZoneId = zoneId;
        SelectedLayoutId = TargetLayout?.Id;
        ApplyZoneStyles();
        UpdateConfirmState();
    }

    private Guid? ZoneAt(System.Windows.Point position)
    {
        foreach (var (zoneId, element) in zoneElements)
        {
            var origin = element.TranslatePoint(new System.Windows.Point(0, 0), ZoneHost);
            if (position.X >= origin.X && position.X <= origin.X + element.ActualWidth &&
                position.Y >= origin.Y && position.Y <= origin.Y + element.ActualHeight)
            {
                return zoneId;
            }
        }

        return null;
    }

    private void Zones_DragOver(object sender, System.Windows.DragEventArgs eventArgs)
    {
        if (!eventArgs.Data.GetDataPresent(DragFormat))
        {
            eventArgs.Effects = System.Windows.DragDropEffects.None;
            eventArgs.Handled = true;
            return;
        }

        var zoneId = ZoneAt(eventArgs.GetPosition(ZoneHost));
        HighlightZone(zoneId);
        eventArgs.Effects = zoneId is null ? System.Windows.DragDropEffects.None : System.Windows.DragDropEffects.Link;
        eventArgs.Handled = true;
    }

    private void Zones_DragLeave(object sender, System.Windows.DragEventArgs eventArgs) => HighlightZone(null);

    private void Zones_Drop(object sender, System.Windows.DragEventArgs eventArgs)
    {
        if (!eventArgs.Data.GetDataPresent(DragFormat))
        {
            return;
        }

        var zoneId = ZoneAt(eventArgs.GetPosition(ZoneHost));
        HighlightZone(null);
        if (zoneId is null || eventArgs.Data.GetData(DragFormat) is not string process)
        {
            return;
        }

        SelectedProcessName = process;
        SelectZone(zoneId.Value);
        eventArgs.Handled = true;
    }

    private void Browse_Click(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Programmdatei wählen",
            Filter = "Programme (*.exe)|*.exe|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        // Bewusst nur der Dateiname: er überlebt Updates, die den Installationspfad ändern.
        SelectedProcessName = Path.GetFileName(dialog.FileName);
        ProcessList.SelectedItem = null;
        UpdateConfirmState();
    }

    private void Details_Click(object sender, RoutedEventArgs eventArgs)
    {
        OpenDetails = true;
        if (!ConfirmButton.IsEnabled)
        {
            SelectionText.Text = "Wähle zuerst ein Fenster und eine Zone; das Detail öffnet sich dann gleich nach dem Zuordnen.";
            return;
        }

        Confirm();
    }

    private void Confirm_Click(object sender, RoutedEventArgs eventArgs) => Confirm();

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs) => DialogResult = false;

    private void Confirm()
    {
        if (!ConfirmButton.IsEnabled)
        {
            return;
        }

        if (mode == Mode.Assign)
        {
            SelectedLayoutId = TargetLayout?.Id;
        }

        DialogResult = true;
    }

    private void UpdateConfirmState()
    {
        var hasProcess = !string.IsNullOrWhiteSpace(SelectedProcessName);
        var ready = mode == Mode.Assign ? hasProcess && SelectedZoneId is not null && TargetLayout is not null : hasProcess;
        ConfirmButton.IsEnabled = ready;
        if (mode != Mode.Assign)
        {
            return;
        }

        var zoneName = TargetLayout?.Zones.FirstOrDefault(zone => zone.Id == SelectedZoneId)?.Name;
        SelectionText.Text = (hasProcess, zoneName) switch
        {
            (true, { } name) => $"{SelectedProcessName} → {name}. «Zuordnen» legt die Zuordnung an.",
            (true, null) => $"{SelectedProcessName} gewählt – zieh es auf eine Zone oder klicke sie an.",
            (false, { } name) => $"Zone «{name}» gewählt – wähle links das Fenster dazu.",
            _ => "Wähle links ein Fenster und zieh es auf eine Zone – oder klicke eine Zone an."
        };
    }
}
