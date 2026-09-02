using System.Windows.Controls;
using SnapZones.App.Services;
using SnapZones.App.Views;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class ProcessPickerUiTests
{
    private static readonly RunningProcessEntry[] Sample =
    [
        new("Explorer.exe", @"C:\Windows\Explorer.exe", "Dokumente"),
        new("Teams.exe", @"C:\Apps\Teams.exe", "Chat")
    ];

    [Fact]
    public void Picker_lists_every_supplied_process_and_starts_without_a_confirmation()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new ProcessPickerWindow(Sample);
            var list = Assert.IsType<ListBox>(window.FindName("ProcessList"));
            var confirm = Assert.IsType<Button>(window.FindName("ConfirmButton"));

            Assert.Equal(2, list.Items.Count);
            Assert.False(confirm.IsEnabled);
            Assert.Null(window.SelectedProcessPath);
        });
    }

    [Fact]
    public void Picker_narrows_the_list_while_typing_and_enables_the_confirmation_on_selection()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new ProcessPickerWindow(Sample);
            var list = Assert.IsType<ListBox>(window.FindName("ProcessList"));
            var search = Assert.IsType<TextBox>(window.FindName("SearchText"));
            var confirm = Assert.IsType<Button>(window.FindName("ConfirmButton"));

            search.Text = "teams";

            Assert.Equal(
                "Teams.exe",
                Assert.IsType<RunningProcessEntry>(Assert.Single(list.Items)).DisplayName);

            list.SelectedIndex = 0;

            Assert.True(confirm.IsEnabled);
        });
    }
}
