using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RouteWatch.Core;

namespace RouteWatch.Desktop;

public partial class MainViewModel : ObservableObject
{
    private readonly ShipmentService _service;
    private IReadOnlyList<Shipment> _branch = [];
    private string _loadedBranch = "none";
    [ObservableProperty] private string _baseAddress = "";
    [ObservableProperty] private string _branchCode = "BR-001";
    [ObservableProperty] private string _selectedStatus = "All";
    [ObservableProperty] private string _message = "Ready.";
    [ObservableProperty] private string _summary = "No branch loaded.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _useDelayTestEndpoint;
    [ObservableProperty] private string _loadedBranchLabel = "No branch loaded";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private int _inTransitCount;
    [ObservableProperty] private int _deliveredCount;
    [ObservableProperty] private int _exceptionCount;
    [ObservableProperty] private int _visibleCount;
    [ObservableProperty] private bool _hasVisibleShipments;
    public ObservableCollection<Shipment> VisibleShipments { get; } = [];
    public string[] StatusOptions { get; } = ["All", .. ShipmentRules.Statuses];
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }
    public Func<string, Task<string?>>? ChooseExportFileAsync { get; set; }

    public MainViewModel(ShipmentService service)
    {
        _service = service;
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsBusy);
    }
    partial void OnIsBusyChanged(bool value) { LoadCommand.NotifyCanExecuteChanged(); RefreshCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedStatusChanged(string value) => ApplyFilter();
    private void ApplyFilter()
    {
        VisibleShipments.Clear();
        foreach (var s in ShipmentReports.Filter(_branch, SelectedStatus)) VisibleShipments.Add(s);
        var counts = ShipmentReports.Summarize(_branch);
        TotalCount = counts.Total; PendingCount = counts.Pending; InTransitCount = counts.InTransit;
        DeliveredCount = counts.Delivered; ExceptionCount = counts.Exception;
        VisibleCount = VisibleShipments.Count; HasVisibleShipments = VisibleCount > 0;
        LoadedBranchLabel = _loadedBranch == "none" ? "No branch loaded" : $"Branch {_loadedBranch}";
        Summary = $"Stored {_loadedBranch} total: {counts.Total} | Pending: {counts.Pending} | In transit: {counts.InTransit} | Delivered: {counts.Delivered} | Exception: {counts.Exception}";
    }
    private async Task LoadStoredAsync(string branch)
    {
        await _service.InitializeAsync();
        _branch = await _service.GetBranchAsync(branch);
        _loadedBranch = branch;
        ApplyFilter();
    }
    private async Task LoadAsync()
    {
        IsBusy = true;
        try { var branch = BranchCode; await LoadStoredAsync(branch); Message = $"Showing saved records for {branch}."; }
        catch (Exception e) { Message = $"Could not load saved records: {e.Message}"; }
        finally { IsBusy = false; }
    }
    private async Task RefreshAsync()
    {
        IsBusy = true;
        var branch = BranchCode;
        try
        {
            await LoadStoredAsync(branch);
            var delayTest = UseDelayTestEndpoint;
            Message = delayTest ? $"Refreshing {branch} using the 10-second delay test..." : $"Refreshing {branch}...";
            var count = await _service.RefreshAsync(BaseAddress, branch, useDelayTestEndpoint: delayTest);
            await LoadStoredAsync(branch);
            Message = $"{(delayTest ? "Delay test" : "Refresh")} succeeded for {branch}: {count} received. Showing saved records.";
        }
        catch (Exception e) { Message = $"Refresh failed: {e.Message} Showing previously saved records if available."; }
        finally { IsBusy = false; }
    }
    private async Task ExportAsync()
    {
        IsBusy = true;
        try
        {
            if (VisibleShipments.Count == 0) { Message = "There are no displayed shipments to export."; return; }
            if (ChooseExportFileAsync is null) { Message = "Export is unavailable."; return; }
            var path = await ChooseExportFileAsync("routewatch-shipments.csv");
            if (path is null) { Message = "Export cancelled."; return; }
            await File.WriteAllTextAsync(path, ShipmentReports.Csv(VisibleShipments));
            Message = $"Exported {VisibleShipments.Count} displayed shipments.";
        }
        catch (Exception e) { Message = $"Export failed: {e.Message}"; }
        finally { IsBusy = false; }
    }
}
