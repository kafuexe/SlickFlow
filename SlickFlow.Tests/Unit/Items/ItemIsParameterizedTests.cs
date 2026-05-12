using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace SlickFlow.Tests.Unit.Items;

public class ItemIsParameterizedTests
{
    [Fact]
    public void IsParameterized_True_WhenFileNameHasPlaceholder()
    {
        var item = new Item("1", "http://localhost:<<port>>");
        item.IsParameterized.Should().BeTrue();
    }

    [Fact]
    public void IsParameterized_True_WhenArgumentsHasPlaceholder()
    {
        var item = new Item("1", "script.exe") { Arguments = "--port <<port>>" };
        item.IsParameterized.Should().BeTrue();
    }

    [Fact]
    public void IsParameterized_True_WhenBothHavePlaceholders()
    {
        var item = new Item("1", "<<host>>.exe") { Arguments = "--port <<port>>" };
        item.IsParameterized.Should().BeTrue();
    }

    [Fact]
    public void IsParameterized_False_WhenPlainText()
    {
        var item = new Item("1", "notepad.exe") { Arguments = "--silent" };
        item.IsParameterized.Should().BeFalse();
    }

    [Fact]
    public void IsParameterized_False_WhenEmpty()
    {
        var item = new Item();
        item.IsParameterized.Should().BeFalse();
    }

    [Fact]
    public void IsParameterized_False_ForMetaItem()
    {
        // Meta items use @alias@ syntax, not <<name>>; they're not parameterized.
        var item = new Item("1", "@spotify@@discord@");
        item.IsParameterized.Should().BeFalse();
    }
}
