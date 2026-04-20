# Testing

SysManager ships with two test projects.

## `SysManager.Tests` — unit and integration

xUnit + `CommunityToolkit.Mvvm` test helpers. Covers services, view models,
models, and parsing logic (winget tables, ping samples, event log entries).

Run everything:

```powershell
dotnet test SysManager/SysManager.Tests/SysManager.Tests.csproj -c Release
```

Run a single class:

```powershell
dotnet test SysManager/SysManager.Tests/SysManager.Tests.csproj `
  --filter "FullyQualifiedName~PingMonitorServiceTests"
```

## `SysManager.UITests` — UI automation

FlaUI-driven black-box tests that launch the real `SysManager.exe`, navigate
each tab, and assert on UI elements. Requires a desktop session (won't run on
a headless CI agent without a virtual display).

```powershell
dotnet test SysManager/SysManager.UITests/SysManager.UITests.csproj -c Release
```

## Test layout

- `*ViewModelTests.cs` — happy-path and guard behaviour for each view model.
- `*ExtendedTests.cs` — edge cases, long inputs, malformed data.
- `*ServiceTests.cs` — service logic with injected fakes / temp files.
- `QaAuditTests.cs` / `QaResilienceTests.cs` — cross-cutting smoke sweeps.
- `*UiTests.cs` (UITests project) — per-tab smoke and interaction tests.

## Coverage

Pass `--collect:"XPlat Code Coverage"` to generate a Cobertura report:

```powershell
dotnet test -c Release --collect:"XPlat Code Coverage"
```
