# HoneyPlayBox Console Sample

Interactive sample for `HoneyPlayBox.OpenSDK`, using real BLE toys. Uses the published NuGet package `HoneyPlayBox.OpenSDK` version `1.0.1-beta`, not a local SDK project reference; no simulated transport is used.

## Integration Documentation

- [SDK Quick Start](docs/quick-start.md): install, connect, and execute commands.
- [SDK API Reference](docs/api-reference.md): lifecycle, executors, parameters, and events.

## Run

Requires .NET 9 SDK, a BLE adapter, a compatible toy, and internet access to load the online product configuration.

```powershell
Set-Location "D:\SOLO-Project\JoinHub"
dotnet restore ".\Net Core\sample\HoneyPlayBoxNetCoreSample.csproj"
dotnet run --project ".\Net Core\sample\HoneyPlayBoxNetCoreSample.csproj" `
  -c Release -f net9.0-windows10.0.19041
```

Keep the toy awake and disconnect it from other apps. Use the Windows target above for native BLE. Generic macOS console builds are not a supported CoreBluetooth setup.

## Main Menu

```text
1. Search and connect toys
2. Control connected toys
3. Read battery
4. Disconnect toy
0. Exit
```

Search lists only toys matching the product configuration. Select a toy or enter `a` to connect all.

Command `2` opens the executor menu directly, without a mandatory toy-selection screen:

```text
1. Immediate
2. Funscript
3. Queue
0. Back
```

The current target name is displayed. With multiple toys, `t` switches the target. Battery and disconnect commands have their own toy-selection menu.

## Immediate

| Command | Action |
| --- | --- |
| `1` | Vibrate 50 for 1 second |
| `2` | Vibrate 50 for 10 seconds |
| `3` | Selected-motor vibration 50 for 3 seconds |
| `4` | Constrict 50 for 1 second |
| `5` | Position: center 50, amplitude 30, speed 5 for 1 second |
| `6` | Stop immediate output |
| `p` / `e` / `x` | Read / enable / disable pressure mode |
| `0` | Back |

Unsupported vibration, Constrict, and Position entries are hidden. Pressure commands depend on firmware support.

## Queue

| Command | Action |
| --- | --- |
| `1` | Enqueue vibration 50 for 1 second |
| `2` | Enqueue selected-motor vibration 50 for 3 seconds |
| `3` | Enqueue Constrict 50 for 1 second |
| `4` | Enqueue Position for 1 second |
| `5` | Show queue |
| `6` | Remove a waiting command by command ID |
| `7` | Clear queue |
| `0` | Back |

Unsupported function entries are hidden. Commands execute sequentially.

## Funscript

| Command | Action |
| --- | --- |
| `1` | Load a local file or HTTP/HTTPS URL |
| `2` | Start from zero when Ready or Stopped |
| `3` | Pause |
| `4` | Resume from the paused position |
| `5` | Stop and reset position |
| `6` | Restart from zero |
| `7` | Seek to absolute time (ms) |
| `8` | Jump by relative offset (ms), including negative values |
| `9` | Set speed (0.1x-4x) |
| `t` | Synchronize to external time (ms) |
| `s` | Show status and track matching |
| `0` | Back |

Example path for command `1`:

```text
D:\SOLO-Project\JoinHub\Net Core\sample\vibrate-constrict.funscript
```

Loading does not start playback. Enter `2` to start, `3` to pause, and `4` to resume. This script starts with 2 seconds of zero vibration; unsupported tracks are skipped.

Playback holds original values with a default minimum send interval of 100ms. Returning to a menu does not stop execution. Another executor takes over previous execution on the same toy; different toys remain independent.

## Notes

- Logs show decoded command summaries and feedback, not raw protocol frames.
- Progress measures timeline time, not confirmation of physical output.
- After a Windows BLE write timeout, disconnect and reconnect before retrying. Write-only characteristics cannot use WriteWithoutResponse.
- Stop the sample and Visual Studio debugging before rebuilding to avoid locked DLLs.
- Successful compilation does not confirm real-device compatibility.
- Local SDK source changes do not affect this sample until a new package version is published and its `PackageReference` version is updated.
