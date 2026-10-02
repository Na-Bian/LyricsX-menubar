using Microsoft.UI.Xaml;

namespace LyricsX.Windows.TestMediaApp;

public sealed partial class MainWindow : Window
{
    private readonly MediaScenarioController controller;

    public MainWindow()
    {
        InitializeComponent();
        controller = new MediaScenarioController(
            global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
        controller.Log += message => StatusText.Text = message;

        var pipeName = MediaScenarioController.PipeNameFromCommandLine();
        if (pipeName is not null)
        {
            controller.StartAutomationPipe(pipeName);
        }

        controller.ApplyInitialTrack();
    }

    private void OnPlay(object sender, RoutedEventArgs e) => controller.Play();

    private void OnPause(object sender, RoutedEventArgs e) => controller.Pause();

    private void OnNext(object sender, RoutedEventArgs e) => controller.Next();
}
