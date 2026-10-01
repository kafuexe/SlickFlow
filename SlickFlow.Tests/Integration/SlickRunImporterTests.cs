using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Import;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Integration;

public class SlickRunImporterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly ItemRepository _repo;

    public SlickRunImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SlickFlowImportTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "db.json");
        _repo = new ItemRepository(_dbPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private SlickRunImportResult Import(string qrs, string? iconDir = null)
        => new SlickRunImporter(_repo, iconDir).Import(QrsParser.Parse(qrs));

    [Fact]
    public void Import_AddsNewItemsWithIds()
    {
        var result = Import("[calc]\nFilename=\"calc.exe\"\n[np]\nFilename=\"notepad.exe\"\nParams=\"$W$\"\n");

        result.ImportedCount.Should().Be(2);
        result.Skipped.Should().BeEmpty();
        var items = _repo.GetAllItems();
        items.Should().HaveCount(2);
        items.Should().AllSatisfy(i => i.Id.Should().NotBeNullOrEmpty());
        _repo.GetItemByAlias("np")!.Arguments.Should().Be("<<query>>");
    }

    [Fact]
    public void Import_PersistsToDisk()
    {
        Import("[calc]\nFilename=calc.exe\n");

        new ItemRepository(_dbPath).GetItemByAlias("calc").Should().NotBeNull();
    }

    [Fact]
    public void Import_ExistingAlias_IsSkippedAndExistingItemUntouched()
    {
        var existing = new Item { FileName = "mine.exe", Arguments = "keep", Aliases = new List<string> { "G" } };
        _repo.AddItem(existing);

        var result = Import("[g]\nFilename=\"https://google.com/?q=$W$\"\n[calc]\nFilename=calc.exe\n");

        result.ImportedCount.Should().Be(1);
        result.Skipped.Should().ContainSingle()
            .Which.Should().Match<SkippedEntry>(s => s.Name == "g" && s.Reason == SkipReason.AliasExists);
        var g = _repo.GetItemByAlias("g")!;
        g.Id.Should().Be(existing.Id);
        g.FileName.Should().Be("mine.exe");
        g.Arguments.Should().Be("keep");
        _repo.GetAllItems().Should().HaveCount(2);
    }

    [Fact]
    public void Import_NeverDeletesExistingItems()
    {
        _repo.AddItem(new Item { FileName = "a.exe", Aliases = new List<string> { "a" } });
        _repo.AddItem(new Item { FileName = "b.exe", Aliases = new List<string> { "b" } });

        Import("[c]\nFilename=c.exe\n");

        _repo.GetAllItems().Select(i => i.FileName).Should().Contain(new[] { "a.exe", "b.exe", "c.exe" });
    }

    [Fact]
    public void Import_DuplicateSectionInFile_FirstWinsSecondSkipped()
    {
        var result = Import("[g]\nFilename=first\n[G]\nFilename=second\n");

        result.ImportedCount.Should().Be(1);
        result.Skipped.Should().ContainSingle()
            .Which.Reason.Should().Be(SkipReason.Duplicate);
        _repo.GetItemByAlias("g")!.FileName.Should().Be("first");
    }

    [Fact]
    public void Import_InvalidEntry_IsSkipped()
    {
        var result = Import("[nofile]\nParams=-x\n[ok]\nFilename=ok.exe\n");

        result.ImportedCount.Should().Be(1);
        result.Skipped.Should().ContainSingle()
            .Which.Should().Match<SkippedEntry>(s => s.Name == "nofile" && s.Reason == SkipReason.Invalid);
    }

    [Fact]
    public void Import_MultiAliasEntry_WithPartialConflict_ImportsRemainingAliasesWithWarning()
    {
        _repo.AddItem(new Item { FileName = "mine", Aliases = new List<string> { "amazon" } });

        var result = Import("[AMAZON,WWW.AMAZON.COM]\nFilename=\"https://amazon.com\"\n");

        result.ImportedCount.Should().Be(1);
        result.Skipped.Should().BeEmpty();
        result.Warnings.Should().ContainSingle().Which.Should().Contain("amazon");
        _repo.GetItemByAlias("www.amazon.com")!.FileName.Should().Be("https://amazon.com");
        _repo.GetItemByAlias("amazon")!.FileName.Should().Be("mine");
    }

    [Fact]
    public void Import_MultiAliasEntry_AllAliasesConflict_IsSkipped()
    {
        _repo.AddItem(new Item { FileName = "mine", Aliases = new List<string> { "a", "b" } });

        var result = Import("[a,b]\nFilename=x\n");

        result.ImportedCount.Should().Be(0);
        result.Skipped.Should().ContainSingle().Which.Reason.Should().Be(SkipReason.AliasExists);
    }

    [Fact]
    public void Import_CollectsMapperWarnings()
    {
        var result = Import("[odd]\nFilename=run.exe\nParams=$D$\n");

        result.Warnings.Should().ContainSingle().Which.Should().Contain("$D$");
    }

    [Fact]
    public void Import_IconIsCopiedIntoIconDir_SoDeletingTheItemNeverTouchesTheSource()
    {
        var sourceIcon = Path.Combine(_tempDir, "source.ico");
        File.WriteAllBytes(sourceIcon, new byte[] { 1, 2, 3 });
        var iconDir = Path.Combine(_tempDir, "icons");

        Import($"[x]\nFilename=a.exe\nIcon=\"{sourceIcon}\"\n", iconDir);

        var item = _repo.GetItemByAlias("x")!;
        item.IconPath.Should().StartWith(iconDir);
        File.Exists(item.IconPath).Should().BeTrue();

        _repo.DeleteItem(item.Id);
        File.Exists(sourceIcon).Should().BeTrue();
    }

    [Fact]
    public void Import_IconWithoutIconDir_IsNotLinked()
    {
        var sourceIcon = Path.Combine(_tempDir, "source.ico");
        File.WriteAllBytes(sourceIcon, new byte[] { 1 });

        Import($"[x]\nFilename=a.exe\nIcon=\"{sourceIcon}\"\n");

        _repo.GetItemByAlias("x")!.IconPath.Should().BeEmpty();
    }

    [Fact]
    public void ImportFile_ReadsAndImports()
    {
        var qrs = Path.Combine(_tempDir, "pack.qrs");
        File.WriteAllText(qrs, "[calc]\r\nFilename=\"calc.exe\"\r\nStartMode=5\r\n");

        var result = new SlickRunImporter(_repo).ImportFile(qrs);

        result.ImportedCount.Should().Be(1);
        _repo.GetItemByAlias("calc")!.FileName.Should().Be("calc.exe");
    }

    [Fact]
    public void ToSummary_ListsImportedAndSkippedByReason()
    {
        _repo.AddItem(new Item { FileName = "x", Aliases = new List<string> { "g", "mail" } });

        var result = Import("[g]\nFilename=a\n[mail]\nFilename=b\n[calc]\nFilename=c\n[calc]\nFilename=d\n[bad]\n");

        var summary = result.ToSummary();
        summary.Should().StartWith("Imported 1, skipped 4");
        summary.Should().Contain("alias already exists: g, mail");
        summary.Should().Contain("duplicate in file: calc");
        summary.Should().Contain("invalid: bad");
    }

    [Fact]
    public void ToSummary_NothingSkipped()
    {
        Import("[a]\nFilename=a\n").ToSummary().Should().StartWith("Imported 1, skipped 0");
    }

    [Fact]
    public void ToSummary_IncludesWarnings()
    {
        Import("[odd]\nFilename=run.exe\nParams=$D$\n").ToSummary().Should().Contain("$D$");
    }
}
