using System.Windows;
using System.Windows.Threading;

namespace UWPHook.Windows.Tests.Ui;

/// <summary>
/// One STA thread running a WPF dispatcher, with UWPHook's <see cref="App"/> (and so App.xaml's resources)
/// loaded on it, as <see cref="Program"/> does but without <c>Run</c>. A process holds one Application,
/// and WPF objects belong to the thread that made them, so every UI test runs its work here.
/// </summary>
internal static class UiThread
{
    private static readonly Lazy<Dispatcher> UiDispatcher = new(Start);

    public static T Run<T>(Func<T> work) => UiDispatcher.Value.Invoke(work);

    public static void Run(Action work) => UiDispatcher.Value.Invoke(work);

    private static Dispatcher Start()
    {
        var started = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            try
            {
                // Application.ResourceAssembly stays the test host (WPF only lets a process without an entry
                // assembly change it), so the XAML must name its own resources relative to itself.
                new App().InitializeComponent();
                started.SetResult(System.Windows.Threading.Dispatcher.CurrentDispatcher);
            }
            catch (Exception e)
            {
                started.SetException(e);
                return;
            }

            System.Windows.Threading.Dispatcher.Run();
        })
        { IsBackground = true, Name = "WPF UI tests" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return started.Task.GetAwaiter().GetResult();
    }
}
