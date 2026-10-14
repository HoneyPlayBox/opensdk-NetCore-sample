# HoneyPlayBox Console Sample

Interactive sample for `HoneyPlayBox.OpenSDK`, using real BLE toys or a USB HID gateway. This checkout references the local SDK project, including its new HID support; no simulated transport is used.

## Integration Documentation

- [SDK Introduction](http://127.0.0.1:5173/docs#net-sdk)
- [SDK Quick Start](http://127.0.0.1:5173/docs?nav=winsdk-for-csharp-nav.json#winsdk-csharp-quick-start): install, connect, and execute commands.
- [SDK API Reference](http://127.0.0.1:5173/docs?nav=winsdk-for-csharp-nav.json#winsdk-csharp-api-reference): lifecycle, executors, parameters, and events.

Integration documentation is maintained in the OpenSDKDocument project, not this sample repository. These links use its local development server; use the deployed documentation origin when sharing them publicly.

## Run

Requires .NET 9 SDK, a BLE adapter or compatible USB HID gateway, a compatible toy, and internet access to load the online product configuration.

```powershell
# Run from this sample directory with the sibling SDK source available.
dotnet restore ".\HoneyPlayBoxNetCoreSample.csproj"
dotnet run --project ".\HoneyPlayBoxNetCoreSample.csproj" `
  -c Release -f net9.0-windows10.0.19041
```

Keep the toy awake and disconnect it from other apps. Use the Windows target above for native BLE. Generic macOS console builds are not a supported CoreBluetooth setup.

## Main Menu

The first screen selects the connection type:

```text
Connection type
1. HID
2. BLE
0. Exit
```

HID automatically opens a single matching HPB USB gateway using the same default identifiers as the Python SDK: `3352:5B32` or `1A86:E429`. It does not list unrelated system HID devices or require a second selection menu. If multiple supported interfaces exist, set `Hid.DevicePath` explicitly. Toy discovery then uses the gateway radio, without checking the computer's BLE adapter.

BLE uses the computer's Bluetooth adapter and the existing direct discovery/connection flow. A failed HID connection returns to the connection-type menu. Exit disconnects toys and closes the HID gateway when selected.

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
.\vibrate-constrict.funscript
```

Loading does not start playback. Enter `2` to start, `3` to pause, and `4` to resume. This script starts with 2 seconds of zero vibration; unsupported tracks are skipped.

Playback holds original values with a default minimum send interval of 100ms. Returning to a menu does not stop execution. Another executor takes over previous execution on the same toy; different toys remain independent.

## Notes

- Logs show decoded command summaries and feedback, not raw protocol frames.
- Progress measures timeline time, not confirmation of physical output.
- After a Windows BLE write timeout, disconnect and reconnect before retrying. Write-only characteristics cannot use WriteWithoutResponse.
- Stop the sample and Visual Studio debugging before rebuilding to avoid locked DLLs.
- Successful compilation does not confirm real-device compatibility.
- This checkout uses a local `ProjectReference`. A standalone NuGet-based sample must reference a newly published SDK version containing HID support; the old `1.0.1-beta` package does not include these new APIs.
- See the [API Reference](http://127.0.0.1:5173/docs?nav=winsdk-for-csharp-nav.json#winsdk-csharp-api-reference) for integration contracts, configuration dependencies, and validation boundaries.
