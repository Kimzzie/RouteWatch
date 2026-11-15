using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace RouteWatch.Desktop;

public partial class MainWindow : Window
{
    private static readonly IBrush EvenRow = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush OddRow = new SolidColorBrush(Color.Parse("#F5F8FC"));

    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            vm.ChooseExportFileAsync = async name =>
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export shipments", SuggestedFileName = name, DefaultExtension = "csv" });
                return file?.TryGetLocalPath();
            };
            await vm.LoadCommand.ExecuteAsync(null);
        };
    }

    private void OnShipmentRowLoading(object? sender, DataGridRowEventArgs e) =>
        e.Row.Background = e.Row.Index % 2 == 0 ? EvenRow : OddRow;
}
