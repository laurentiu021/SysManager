using SysManager.ViewModels;

namespace SysManager.Tests;

[Collection("Network")]
public class MainWindowViewModelTests
{
    [Fact]
    public void AllTabsAreInstantiated()
    {
        var vm = new MainWindowViewModel();
        Assert.NotNull(vm.Dashboard);
        Assert.NotNull(vm.AppUpdates);
        Assert.NotNull(vm.WindowsUpdate);
        Assert.NotNull(vm.SystemHealth);
        Assert.NotNull(vm.Cleanup);
        Assert.NotNull(vm.Network);
        Assert.NotNull(vm.Drivers);
        Assert.NotNull(vm.Logs);
    }

    [Fact]
    public void ElevationBadge_IsOneOfTwoValues()
    {
        var vm = new MainWindowViewModel();
        Assert.True(vm.ElevationBadge == "Administrator" || vm.ElevationBadge == "Standard user",
            $"Unexpected badge: {vm.ElevationBadge}");
    }

    [Fact]
    public void Title_NotEmpty()
    {
        var vm = new MainWindowViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.Title));
    }

    [Fact]
    public void Title_ReflectsElevation()
    {
        var vm = new MainWindowViewModel();
        if (vm.IsElevated)
            Assert.Contains("Admin", vm.Title);
        else
            Assert.Equal("SysManager", vm.Title);
    }

    [Fact]
    public void EachTabViewModel_HasCorrectType()
    {
        var vm = new MainWindowViewModel();
        Assert.IsType<DashboardViewModel>(vm.Dashboard);
        Assert.IsType<AppUpdatesViewModel>(vm.AppUpdates);
        Assert.IsType<WindowsUpdateViewModel>(vm.WindowsUpdate);
        Assert.IsType<SystemHealthViewModel>(vm.SystemHealth);
        Assert.IsType<CleanupViewModel>(vm.Cleanup);
        Assert.IsType<NetworkViewModel>(vm.Network);
        Assert.IsType<DriversViewModel>(vm.Drivers);
        Assert.IsType<LogsViewModel>(vm.Logs);
    }

    [Fact]
    public void NavItems_ContainAllEight()
    {
        var vm = new MainWindowViewModel();
        Assert.Equal(8, vm.NavItems.Count);
        var ids = vm.NavItems.Select(n => n.Id).ToList();
        Assert.Contains("nav-dashboard", ids);
        Assert.Contains("nav-network", ids);
        Assert.Contains("nav-logs", ids);
    }

    [Fact]
    public void SelectedNav_DefaultsToDashboard()
    {
        var vm = new MainWindowViewModel();
        Assert.NotNull(vm.SelectedNav);
        Assert.Equal("nav-dashboard", vm.SelectedNav!.Id);
    }

    [Fact]
    public void NavItems_HaveUniqueIds()
    {
        var vm = new MainWindowViewModel();
        var ids = vm.NavItems.Select(n => n.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void NavItems_AllHaveLabelsAndGlyphs()
    {
        var vm = new MainWindowViewModel();
        Assert.All(vm.NavItems, n =>
        {
            Assert.False(string.IsNullOrWhiteSpace(n.Label));
            Assert.False(string.IsNullOrWhiteSpace(n.Glyph));
            Assert.NotNull(n.Content);
            Assert.NotNull(n.ViewType);
        });
    }
}
