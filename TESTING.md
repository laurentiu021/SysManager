# Testing

SysManager ships with two test projects and a manual UI smoke script.

## `SysManager.Tests` — unit and integration

xUnit + `CommunityToolkit.Mvvm` helpers. Covers services, view models,
models, and parsing logic.

Run everything:

```powershell
dotnet test SysManager/SysManager.Tests/SysManager.Tests.csproj -c Release
```

Run a single class:

```powershell
dotnet test SysManager/SysManager.Tests/SysManager.Tests.csproj `
  --filter "FullyQualifiedName~PingMonitorServiceTests"
```

Tests are configured to run **sequentially** (`xunit.runner.json` sets
`parallelizeTestCollections: false` and `maxParallelThreads: 1`) so that
file-system fixtures in `DeepCleanupServiceTests` and
`LargeFileScannerTests` can share temp dirs safely.

## Test layout

- `*ViewModelTests.cs` — happy-path and guard behaviour for each view model.
- `*ExtendedTests.cs` — edge cases, long inputs, malformed data.
- `*ServiceTests.cs` — service logic with fakes / temp dirs.
- `UpdateServiceTests.cs` — GitHub API client (network calls cancellable).
- `AboutViewModelTests.cs` — version label, command presence, no-op guards.
- `DeepCleanupServiceTests.cs` — category discovery, opt-in clean, safe
  delete. Uses throw-away temp folders, never touches real user data.
- `LargeFileScannerTests.cs` — read-only sweep, min-size / top-N, cancel.
- `DeepCleanupViewModelTests.cs` — view model contract, scan flow, paths.
- `FixedDriveServiceTests.cs` — drive discovery, filesystem filtering.
- `CleanupCategoryHumanSizeBulkTests.cs` — `HumanSize` matrix coverage
  across unit boundaries (B → KB → MB → GB → TB).
- `UpdateServiceParseVersionBulkTests.cs` — parameterised version parsing.
- `QaAuditTests.cs` / `QaResilienceTests.cs` — cross-cutting smoke sweeps.
- `*UiTests.cs` (UITests project) — per-tab smoke and interaction tests.

## `SysManager.UITests` — UI automation

FlaUI-driven black-box tests that launch the real `SysManager.exe`, navigate
each tab, and assert on UI elements. Requires a desktop session.

```powershell
dotnet test SysManager/SysManager.UITests/SysManager.UITests.csproj -c Release
```

## Manual smoke (UIAutomation PowerShell)

For a quick post-build smoke test from PowerShell:

```powershell
$app = Start-Process ".\publish\SysManager.exe" -PassThru
Start-Sleep 3
# Use the Windows Accessibility tree to click every nav tab
# (see docs/manual-smoke.ps1 for the full script)
```

Every nav item in the left rail has a stable `AutomationId`:

- `nav-dashboard`, `nav-app-updates`, `nav-windows-update`,
  `nav-system-health`, `nav-cleanup`, `nav-deep-cleanup`,
  `nav-network`, `nav-drivers`, `nav-logs`, `nav-about`.

## Coverage

Pass `--collect:"XPlat Code Coverage"` to generate a Cobertura report:

```powershell
dotnet test -c Release --collect:"XPlat Code Coverage"
```
