using System.Reflection;
using Xunit;

namespace UWPHook.Windows.Tests;

/// <summary>
/// Velopack's hooks only run if <c>VelopackApp.Build().Run()</c> is the first thing UWPHook.exe does,
/// so the entry point must be our own <c>UWPHook.Program.Main</c>, not the one WPF generates for App.xaml.
/// </summary>
public class StartupTests
{
    private static readonly MethodInfo? EntryPoint = typeof(App).Assembly.EntryPoint;

    [Fact]
    public void EntryPoint_IsProgramMain()
    {
        Assert.NotNull(EntryPoint);
        Assert.Equal("UWPHook.Program", EntryPoint.DeclaringType?.FullName);
        Assert.Equal("Main", EntryPoint.Name);
    }

    [Fact]
    public void EntryPoint_RunsOnAnStaThread_AsWpfRequires()
    {
        Assert.NotNull(EntryPoint);
        Assert.NotNull(EntryPoint.GetCustomAttribute<STAThreadAttribute>());
    }

    [Fact]
    public void EntryPoint_TakesTheCommandLineArguments()
    {
        Assert.NotNull(EntryPoint);
        var parameter = Assert.Single(EntryPoint.GetParameters());
        Assert.Equal(typeof(string[]), parameter.ParameterType);
    }
}
