using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SysManager.Helpers;
using SysManager.Services;

namespace SysManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public DashboardViewModel Dashboard { get; }
    public AppUpdatesViewModel AppUpdates { get; }
    public WindowsUpdateViewModel WindowsUpdate { get; }
    public SystemHealthViewModel SystemHealth { get; }
    public CleanupViewModel Cleanup { get; }
    public NetworkViewModel Network { get; }
    public DriversViewModel Drivers { get; }
    public LogsViewModel Logs { get; }

    public ObservableCollection<NavItem> NavItems { get; } = new();

    [ObservableProperty] private NavItem? _selectedNav;
    [ObservableProperty] private string _title = "SysManager";
    [ObservableProperty] private bool _isElevated;
    [ObservableProperty] private string _elevationBadge = "";

    public MainWindowViewModel()
    {
        var runner = new PowerShellRunner();
        var sysInfo = new SystemInfoService();
        var winget = new WingetService(new PowerShellRunner());

        Dashboard = new DashboardViewModel(sysInfo);
        AppUpdates = new AppUpdatesViewModel(winget);
        WindowsUpdate = new WindowsUpdateViewModel(new PowerShellRunner());
        SystemHealth = new SystemHealthViewModel(sysInfo);
        Cleanup = new CleanupViewModel(new PowerShellRunner());
        Network = new NetworkViewModel();
        Drivers = new DriversViewModel(new PowerShellRunner());
        Logs = new LogsViewModel();

        IsElevated = AdminHelper.IsElevated();
        ElevationBadge = IsElevated ? "Administrator" : "Standard user";
        Title = IsElevated ? "SysManager — Administrator" : "SysManager";

        // Views are instantiated lazily on first access — lets unit tests
        // construct the VM on an MTA thread without pulling WPF resources in.
        NavItems.Add(new NavItem { Id = "nav-dashboard",      Label = "Dashboard",      Glyph = "\uE80F", Content = Dashboard,     ViewType = typeof(Views.DashboardView) });
        NavItems.Add(new NavItem { Id = "nav-app-updates",    Label = "App updates",    Glyph = "\uE7B8", Content = AppUpdates,    ViewType = typeof(Views.AppUpdatesView) });
        NavItems.Add(new NavItem { Id = "nav-windows-update", Label = "Windows Update", Glyph = "\uE895", Content = WindowsUpdate, ViewType = typeof(Views.WindowsUpdateView) });
        NavItems.Add(new NavItem { Id = "nav-system-health",  Label = "System health",  Glyph = "\uE9D9", Content = SystemHealth,  ViewType = typeof(Views.SystemHealthView) });
        NavItems.Add(new NavItem { Id = "nav-cleanup",        Label = "Cleanup",        Glyph = "\uE74D", Content = Cleanup,       ViewType = typeof(Views.CleanupView) });
        NavItems.Add(new NavItem { Id = "nav-network",        Label = "Network",        Glyph = "\uE839", Content = Network,       ViewType = typeof(Views.NetworkView) });
        NavItems.Add(new NavItem { Id = "nav-drivers",        Label = "Drivers",        Glyph = "\uE950", Content = Drivers,       ViewType = typeof(Views.DriversView) });
        NavItems.Add(new NavItem { Id = "nav-logs",           Label = "Logs",           Glyph = "\uE9F9", Content = Logs,          ViewType = typeof(Views.LogsView) });

        SelectedNav = NavItems[0];
    }
}
