# CodexQuota

Native Windows 11 tray overlay for ChatGPT/Codex quota monitoring.

## Status

This is the initial scaffold. The direct usage endpoint is undocumented and may change. The WebView2 login is present; the next implementation step is to copy its authenticated cookies into `UsageClient` and parse the exact live response shape.

## Build

Install the .NET 8 SDK and WebView2 Runtime, then run:

```powershell
dotnet restore
dotnet build
```
