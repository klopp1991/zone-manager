using System.Security.Cryptography;
using SnapZones.Tests.Support;
using SnapZones.Windows.Displays;
using Xunit;

namespace SnapZones.Tests.Monitors;

/// <summary>
/// Das Treiberpaket reist in der Programmdatei mit und muss unveraendert wieder herauskommen; die
/// Ausgabe von pnputil ist je nach Sprache anders beschriftet und darf die Erkennung nicht stoeren.
/// </summary>
public sealed class VirtualDisplayDriverTests
{
    [Fact]
    public void The_embedded_package_is_extracted_unchanged()
    {
        using var directory = new TemporaryDirectory();

        var infPath = VirtualDisplayDriverPackage.Extract(directory.Path);

        Assert.Equal(Path.Combine(directory.Path, "MttVDD.inf"), infPath);
        Assert.Equal(4132, new FileInfo(infPath).Length);
        Assert.Equal(251640, new FileInfo(Path.Combine(directory.Path, "MttVDD.dll")).Length);
        Assert.Equal(13017, new FileInfo(Path.Combine(directory.Path, "mttvdd.cat")).Length);
        Assert.Contains("MIT License", File.ReadAllText(Path.Combine(directory.Path, "LICENSE")));

        // Die INF traegt die Hardware-Kennung, unter der Zone Manager den Knoten anlegt.
        Assert.Contains(@"Root\MttVDD", File.ReadAllText(infPath));

        using var stream = VirtualDisplayDriverPackage.Open("MttVDD.dll");
        var header = new byte[2];
        Assert.Equal(2, stream.Read(header, 0, 2));
        Assert.Equal("MZ", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public void Published_inf_names_are_found_in_german_and_english_pnputil_output()
    {
        const string german = """
            Microsoft-PnP-Dienstprogramm

            Veröffentlichter Name:     oem32.inf
            Originalname:      xvdd.inf
            Anbietername:      Xbox
            Klassenname:         SCSIAdapter

            Veröffentlichter Name:     oem189.inf
            Originalname:      mttvdd.inf
            Anbietername:      MikeTheTech
            Klassenname:         Display
            Klassen-GUID:         {4d36e968-e325-11ce-bfc1-08002be10318}
            Treiberversion:     12/24/2024 11.30.4.434

            Veröffentlichter Name:     oem90.inf
            Originalname:      tvvirtualmonitordriver.inf
            """;
        const string english = """
            Published Name:     oem7.inf
            Original Name:      MttVDD.inf
            Provider Name:      MikeTheTech
            Class Name:         Display

            Published Name:     oem8.inf
            Original Name:      other.inf
            """;

        Assert.Equal(["oem189.inf"], VirtualDisplayDriverService.ParsePublishedInfNames(german, "MttVDD.inf"));
        Assert.Equal(["oem7.inf"], VirtualDisplayDriverService.ParsePublishedInfNames(english, "MttVDD.inf"));
        Assert.Empty(VirtualDisplayDriverService.ParsePublishedInfNames("", "MttVDD.inf"));
    }

    [Fact]
    public void Reading_the_status_needs_no_rights_and_never_throws()
    {
        var status = VirtualDisplayDriverService.ReadStatus();

        Assert.Equal(status.DevicePresent && status.ConfigurationPresent, status.Ready);
        Assert.Equal(status.DevicePresent || status.DriverStored || status.ConfigurationPresent, status.AnythingPresent);
    }
}
