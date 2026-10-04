using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Import;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Integration;

public class SlickRunImporterBuiltInTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ItemRepository _repo;

    public SlickRunImporterBuiltInTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SlickFlowImportBuiltIn_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _repo = new ItemRepository(Path.Combine(_tempDir, "db.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private SlickRunImportResult Import(string qrs)
        => new SlickRunImporter(_repo, null).Import(QrsParser.Parse(qrs));

    // Shape of the entries in a real SlickRun.srl.
    private const string RealSample = """
        [MAIL]
        Filename="https://outlook.live.com/"
        Path=""
        Params=""
        Notes="Microsoft Outlook Web Access e-mail reader"
        StartMode=5
        [MORNING]
        Filename="@MULTI@"
        Params="https://news.google.com@MAIL@notepad.exe"
        StartMode=5
        [CALENDAR]
        Filename="@calendar@"
        [SHUTDOWN]
        Filename="@SHUTDOWN@"
        """;

    [Fact]
    public void Multi_ImportsAsMetaItemWhoseChainResolves()
    {
        var result = Import(RealSample);

        result.Skipped.Select(s => s.Name).Should().Equal("CALENDAR");
        var morning = _repo.GetItemByAlias("morning")!;
        morning.FileName.Should().Be("@https://news.google.com@@mail@@notepad.exe@");

        var leaves = new List<Item>();
        morning.WalkMetaChain(_repo, new HashSet<Item>(), leaves.Add);
        leaves.Select(l => l.FileName).Should().Equal(
            "https://news.google.com", "https://outlook.live.com/", "notepad.exe");
    }

    [Fact]
    public void Multi_ReferringToMagicWordDefinedLater_DoesNotWarn()
    {
        var result = Import("[m]\nFilename=\"@MULTI@\"\nParams=\"later\"\n[later]\nFilename=calc.exe\n");

        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Multi_ReferringToExistingSlickFlowAlias_DoesNotWarn()
    {
        _repo.AddItem(new Item { FileName = "calc.exe", Aliases = new List<string> { "calc" } });

        var result = Import("[m]\nFilename=\"@MULTI@\"\nParams=\"CALC\"\n");

        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Multi_ReferringToUnknownMagicWord_IsImportedWithWarning()
    {
        var result = Import("[m]\nFilename=\"@MULTI@\"\nParams=\"ghost@notepad.exe\"\n");

        result.ImportedCount.Should().Be(1);
        result.Warnings.Should().ContainSingle().Which.Should().Contain("ghost").And.Contain("m");
    }

    [Fact]
    public void UnsupportedBuiltIn_IsSkippedAndNamedInSummary()
    {
        var result = Import(RealSample);

        result.Skipped.Should().ContainSingle()
            .Which.Reason.Should().Be(SkipReason.Unsupported);
        result.ToSummary().Should().Contain("no SlickFlow equivalent: CALENDAR");
    }

    [Fact]
    public void PowerCommand_IsImported()
    {
        Import(RealSample);

        var shutdown = _repo.GetItemByAlias("shutdown")!;
        shutdown.FileName.Should().Be("shutdown.exe");
        shutdown.Arguments.Should().Be("/s /t 0");
    }
}
