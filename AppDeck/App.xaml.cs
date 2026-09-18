using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AppDeck;

public partial class App : Application
{
    private MainWindow? _window;

    public static App? CurrentApp => Current as App;
    public DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_window is null)
            _window = new MainWindow();

        _window.Activate();
    }
}