using FluentAssertions;
using Flow.Launcher.Plugin.SlickFlow.Utils;

namespace SlickFlow.Tests.Unit.Utils;

public class RelayCommandTests
{
    [Fact]
    public void Execute_InvokesAction()
    {
        object? received = null;
        var cmd = new RelayCommand(p => received = p);

        cmd.Execute("hello");

        received.Should().Be("hello");
    }

    [Fact]
    public void Execute_WithNullParameter_InvokesAction()
    {
        var invoked = false;
        var cmd = new RelayCommand(_ => invoked = true);

        cmd.Execute(null);

        invoked.Should().BeTrue();
    }

    [Fact]
    public void CanExecute_ReturnsTrue_WhenNoCanExecuteProvided()
    {
        var cmd = new RelayCommand(_ => { });

        cmd.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void CanExecute_DelegatesToCanExecuteFunc()
    {
        var cmd = new RelayCommand(_ => { }, p => p is string s && s == "yes");

        cmd.CanExecute("yes").Should().BeTrue();
        cmd.CanExecute("no").Should().BeFalse();
        cmd.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void RaiseCanExecuteChanged_FiresEvent()
    {
        var cmd = new RelayCommand(_ => { });
        var fired = false;
        cmd.CanExecuteChanged += (_, _) => fired = true;

        cmd.RaiseCanExecuteChanged();

        fired.Should().BeTrue();
    }

    [Fact]
    public void RaiseCanExecuteChanged_DoesNotThrow_WhenNoSubscribers()
    {
        var cmd = new RelayCommand(_ => { });

        var act = () => cmd.RaiseCanExecuteChanged();

        act.Should().NotThrow();
    }
}
