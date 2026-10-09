# SDK API Reference

Application-facing API for `HoneyPlayBox.OpenSDK`. Prefer the three executors for command execution.
Namespaces: `HoneyPlayBox.OpenSDK`, `.Executors`, `.Models`, and `.Enums`.
Examples use `var sdk = new ToyConnector();` and a connected `toyId`.

Async methods below accept an optional `CancellationToken cancellationToken = default` unless noted otherwise.

## Connector

| Member | Return | Purpose |
| --- | --- | --- |
| `new ToyConnector(ToyConnectorOptions? options = null)` | `ToyConnector` | Create shared connection owner |
| `Immediate` | `ImmediateExecutor` | Lazy immediate executor |
| `Queue` | `QueueExecutor` | Lazy sequential executor |
| `Funscript` | `FunscriptExecutor` | Lazy timeline executor |
| `IsBleSupportedAsync()` (static) / `IsSupportedAsync()` | `Task<SupportState>` | Check platform BLE support; no cancellation argument |
| `DiscoverDevicesAsync()` | `Task<IReadOnlyList<ToyInfo>>` | Discover devices matching product configuration |
| `ConnectDeviceAsync(string toyId)` | `Task<Toy>` | Connect and initialize a toy |
| `ConnectDevicesAsync(IEnumerable<string> toyIds)` | `Task<IReadOnlyList<Toy>>` | Connect multiple toys |
| `GetConnectedToys()` | `IReadOnlyList<Toy>` | Current connections |
| `GetToy(string toyId)` | `Toy?` | Lookup a connected toy |
| `ReadBatteryLevelAsync(string toyId)` | `Task<int?>` | Battery percentage; null means unavailable |
| `DisconnectDeviceAsync(string toyId)` | `Task` | Disconnect a toy |

`ToyConnectorOptions`: `AutoReconnect=true`, `ReconnectMaxAttempts=5`, `ReconnectDelayMs=1000`, `ReconnectMaxDelayMs=10000`.

## Shared Executor Methods

All three executors inherit these methods from `ToyExecutorBase` and use the same connections.

| Method | Return |
| --- | --- |
| `ScanAsync()` | `Task<IReadOnlyList<ToyInfo>>` |
| `ConnectAsync(string toyId)` | `Task<Toy>` |
| `ConnectAsync(IEnumerable<string> toyIds)` | `Task<IReadOnlyList<Toy>>` |
| `DisconnectAsync(string toyId)` | `Task` |
| `GetConnectedToys()` | `IReadOnlyList<Toy>` |
| `GetToy(string toyId)` | `Toy?` |
| `ReadBatteryAsync(string toyId)` | `Task<int?>` |

## Immediate

| Method | Return | Behavior |
| --- | --- | --- |
| `ExecuteAsync(string toyId, VibrationAction command)` | `Task<VibrationResult>` | Send one action |
| `ExecuteAsync(string toyId, IReadOnlyList<VibrationAction> commands)` | `Task<VibrationResult>` | Send a combined action list, not a sequential queue |
| `StopAsync(string toyId)` | `Task<VibrationResult>` | Stop current output |

Completion means transport submission completed, not that the action duration elapsed or physical output was verified.

## Queue

| Method | Return | Behavior |
| --- | --- | --- |
| `ExecuteAsync(string toyId, VibrationAction command)` | `Task<VibrationResult>` | Enqueue one action |
| `ExecuteAsync(string toyId, IReadOnlyList<VibrationAction> commands)` | `Task<VibrationResult>` | Enqueue one combined action group |
| `GetItems(string toyId)` | `IReadOnlyList<ToyCommandQueueItem>` | Snapshot of running/waiting entries |
| `Remove(string toyId, string commandId)` | `bool` | Remove waiting entry; false for missing/running entry |
| `Clear(string toyId, string reason = "clear")` | `int` | Clear queue, returning removed entry count |

`GetItems`, `Remove`, and `Clear` are synchronous and have no cancellation parameter.
Awaiting `ExecuteAsync` waits for enqueueing, not execution completion. Use positive durations for sequential timed actions.
`Clear` does not itself transmit a stop frame. Use `await sdk.Immediate.StopAsync(toyId)` when physical output must stop.

Queue items expose `CommandId`, `ToyId`, `Name`, `Commands`, `CommandCount`, `QueuedAt`, `Position`, and `Status` (`running`/`waiting`).

## Funscript

| Method | Return | Behavior |
| --- | --- | --- |
| `Load(string toyId, string json, FunscriptPlaybackOptions? options = null)` | `string` | Validate/load JSON and return original content |
| `Load(string toyId, FunscriptDocument document, FunscriptPlaybackOptions? options = null)` | `string` | Serialize/load document and return JSON |
| `LoadAsync(string toyId, string source, FunscriptPlaybackOptions? options = null)` | `Task<string>` | Load JSON or HTTP/HTTPS URL; return actual JSON |
| `StartAsync(string toyId)` | `Task<FunscriptPlaybackStatus>` | Start Ready/Stopped playback; use Resume for Paused |
| `PauseAsync(string toyId)` | `Task<FunscriptPlaybackStatus>` | Stop output and preserve position |
| `ResumeAsync(string toyId)` | `Task<FunscriptPlaybackStatus>` | Continue Paused playback |
| `StopAsync(string toyId)` | `Task<FunscriptPlaybackStatus>` | Stop output and reset position |
| `RestartAsync(string toyId)` | `Task<FunscriptPlaybackStatus>` | Restart from zero, including after Ended |
| `SeekAsync(string toyId, double positionMs)` | `Task<FunscriptPlaybackStatus>` | Seek to absolute script time |
| `JumpAsync(string toyId, double offsetMs)` | `Task<FunscriptPlaybackStatus>` | Relative seek; negative offsets allowed |
| `SetPlaybackRateAsync(string toyId, double playbackRate)` | `Task<FunscriptPlaybackStatus>` | Set speed, 0.1-4.0 |
| `SynchronizeTimeAsync(string toyId, double authoritativeTimeMs)` | `Task<FunscriptPlaybackStatus>` | Align to an external timeline |
| `GetStatus(string toyId)` | `FunscriptPlaybackStatus` | Read playback/matching snapshot |
| `GetDocument(string toyId)` | `FunscriptDocument?` | Read parsed source document |

`Load`, `GetStatus`, and `GetDocument` are synchronous and have no cancellation parameter.
Local paths must be read with `File.ReadAllTextAsync` before loading. Loading alone does not start playback.

### Options And Status

| Option | Default | Meaning |
| --- | --- | --- |
| `Function` | null | Optional function selection |
| `Motor` | null | Optional motor selection |
| `Amplitude` | 30 | Position amplitude |
| `Speed` | 10 | Position speed |
| `MinSendIntervalMs` | 100 | Positive minimum real-time interval between action frames |

Dense actions coalesce to current values; this is not a fixed periodic send rate. Stop commands bypass the interval.

Status exposes `ToyId`, `Name`, `State`, `Reason`, `PositionMs`, `DurationMs`, `PlaybackRate`, `Progress` (0-1), `ActionCount`, `Tracks`, `LastSentAt`, `DriftMs`, `Message`, and `Time`.
States: `Empty`, `Ready`, `Playing`, `Paused`, `Stopped`, `Ended`, `Disconnected`, `Error`.
Each track match exposes `TrackId`, `Function`, `Motor`, `Matched`, and `Reason`.

### Multi-Function Scripts

```json
{
  "version": "1.0",
  "tracks": [
    {
      "id": "vibration",
      "function": "Vibrate",
      "actions": [{ "at": 0, "pos": 30 }, { "at": 1000, "pos": 0 }]
    },
    {
      "id": "constrict",
      "function": "Constrict",
      "actions": [{ "at": 0, "pos": 40 }, { "at": 1000, "pos": 0 }]
    }
  ]
}
```

`at` is milliseconds and `pos` is 0-100. Omit `motor` to allow product configuration to select compatible motors.
Unsupported tracks are skipped; conflicting assignments are rejected rather than silently overriding each other.
`FunscriptDocument.Parse(json)` and `ToJson()` preserve metadata and extension fields; preserved fields do not necessarily drive playback.

## Actions And Capabilities

`VibrationAction` is used for all supported functions, not only vibration.

| Field | Meaning |
| --- | --- |
| `Function` | `ToyFunctions`; defaults to `Vibrate` |
| `Value` | Requested strength/value; generally 0-100, subject to toy limits |
| `Motor` | Motor selection; leave default to use SDK/config mapping |
| `Target` | Optional named motor alias |
| `DurationMs` | Duration in milliseconds; prefer this explicit field |
| `Duration` | Legacy duration in milliseconds, used when DurationMs is null |
| `CenterPosition`, `Amplitude`, `Speed` | Position command parameters |
| `WorkMode`, `FeatureWorkMode` | Optional device work-mode selection |

Compatibility aliases include `Strength`, `Vibration`, `VibrationStrength`, `Center`, `Position`, and `AmplitudeStrength`. Prefer explicit primary fields in new integrations.

`Toy` exposes `ToyId`, `Name`, `BluetoothName`, `Connected`, `BatteryLevel`, `Features`, `FunctionMotors`, `FeatureSettings`, and `MotorAliases`.
Check `toy.Features.Contains(ToyFunctions.Constrict)` before exposing Constrict controls. Protocol type is not a toy capability.
The function enum includes `Vibrate`, `Rotate`, `Oscillate`, `Constrict`, `Spray`, `Temperature`, `Led`, `Position`, and `HwPositionWithDuration`; not every toy supports every value.

Pressure APIs on `Toy`: `ReadPressureModeAsync()` returns `Task<bool>`; `SetPressureModeAsync(bool enabled)` returns `Task<DeviceCommandResult>`. Firmware support is required.

## Events

Subscribe on the connector before scanning or executing commands.

| Event | Payload | Purpose |
| --- | --- | --- |
| `Discover` | `ToyInfo` | Matched discovery |
| `Connect` | `ConnectState` | Connection state changes |
| `Disconnect` | `ToyState` | Disconnection |
| `Feedback` | `ToyFeedback` | Decoded device feedback, including battery/pressure |
| `Command` | `VibrationResult` | Command submission result |
| `QueueChange` | `ToyCommandQueueChange` | Queue changes |
| `FunscriptStateChanged` | `FunscriptPlaybackStatus` | Playback state/progress/errors |
| `Error` | `ToyError` | SDK errors |

Keep handlers short; marshal to your application's UI thread when needed. Do not print raw data in normal application logs.
`IToyTransport` is internal, not an application integration interface. This guide intentionally excludes raw-frame/protocol APIs.

## Execution And Errors

- Executors share one connector but maintain independent execution per toy.
- Later execution on the same toy replaces previous execution from another executor; consecutive Queue submissions remain sequential.
- Handle exceptions from awaited methods as well as the `Error` event.
- Connection includes protocol initialization; discovering a toy does not guarantee connection success.
- A BLE write timeout requires disconnect/reconnect before retrying on the failed connection.
- Local SDK changes do not update a NuGet-based application until its package reference is updated.

See [Quick Start](quick-start.md) for a complete connection example.
