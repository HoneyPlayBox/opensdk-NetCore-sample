# SDK Quick Start

Integrate `HoneyPlayBox.OpenSDK` through NuGet. This sample references `1.0.1-beta`.

## Install

Use .NET 9 and the Windows target `net9.0-windows10.0.19041` for native Windows BLE.
Keep the toy awake and disconnected from other apps. Internet access is required for product configuration.

```powershell
dotnet new console -n ToyDemo
Set-Location ToyDemo
dotnet add package HoneyPlayBox.OpenSDK --version 1.0.1-beta
```

In `ToyDemo.csproj`, replace the generated target framework:

```xml
<TargetFramework>net9.0-windows10.0.19041</TargetFramework>
```

## Connect And Vibrate

Replace `Program.cs` with:

```csharp
using HoneyPlayBox.OpenSDK;
using HoneyPlayBox.OpenSDK.Enums;
using HoneyPlayBox.OpenSDK.Models;

var sdk = new ToyConnector();
sdk.Error += (_, error) => Console.WriteLine(error.Message);
sdk.Feedback += (_, feedback) =>
    Console.WriteLine($"{feedback.ToyId}: {feedback.Kind}={feedback.Value}");

var devices = await sdk.Immediate.ScanAsync();
if (devices.Count == 0)
{
    Console.WriteLine("No toys matched product config.");
    return;
}

var toy = await sdk.Immediate.ConnectAsync(devices[0].ToyId);
try
{
    var battery = await sdk.Immediate.ReadBatteryAsync(toy.ToyId);
    Console.WriteLine($"{toy.Name}: battery={battery?.ToString() ?? "unknown"}%");

    // Check capabilities instead of assuming every toy has the same functions.
    if (toy.Features.Contains(ToyFunctions.Vibrate))
    {
        await sdk.Immediate.ExecuteAsync(toy.ToyId, new VibrationAction
        {
            Function = ToyFunctions.Vibrate,
            Value = 50,
            DurationMs = 1_000
        });
        // ExecuteAsync returns after submission, not after the duration elapses.
        await Task.Delay(1_200);
        await sdk.Immediate.StopAsync(toy.ToyId);
    }
}
finally
{
    await sdk.Immediate.DisconnectAsync(toy.ToyId);
}
```

Run `dotnet run`. Keep one connector for the application: its executors share connections and are initialized lazily.

## Multiple Toys

```csharp
var devices = await sdk.Immediate.ScanAsync();
var toys = await sdk.Immediate.ConnectAsync(devices.Select(device => device.ToyId));
foreach (var toy in toys)
    Console.WriteLine($"Connected: {toy.Name} ({toy.ToyId})");
```

Control each toy by its `ToyId`. Disconnect each connected toy during application shutdown.

## Queue

```csharp
await sdk.Queue.ExecuteAsync(toy.ToyId,
    new VibrationAction { Value = 30, DurationMs = 1_000 });
await sdk.Queue.ExecuteAsync(toy.ToyId,
    new VibrationAction { Value = 70, DurationMs = 1_000 });
```

Commands run sequentially on the same toy. Awaiting submission does not wait for queue completion.
Use `sdk.Queue.GetItems(toy.ToyId)` and the `QueueChange` event to monitor the queue.

## Funscript

```csharp
sdk.FunscriptStateChanged += (_, status) =>
    Console.WriteLine($"{status.State}: {status.PositionMs:F0}/{status.DurationMs:F0}ms");

var json = await File.ReadAllTextAsync("vibrate-constrict.funscript");
var content = sdk.Funscript.Load(toy.ToyId, json);
await sdk.Funscript.StartAsync(toy.ToyId);

// Call these from your application's playback controls, not immediately in sequence.
// await sdk.Funscript.PauseAsync(toy.ToyId);
// await sdk.Funscript.ResumeAsync(toy.ToyId);
// await sdk.Funscript.SeekAsync(toy.ToyId, 10_000);
// await sdk.Funscript.StopAsync(toy.ToyId);
```

For online scripts, use `await sdk.Funscript.LoadAsync(toy.ToyId, scriptUrl)`.
Both loading methods return script JSON; loading does not start playback. `LoadAsync` does not read local paths: read local files with `File.ReadAllTextAsync`.

Unsupported function tracks are skipped. Check `sdk.Funscript.GetStatus(toy.ToyId).Tracks` for matching results.
New execution from another executor replaces previous execution on the same toy. Different toys are independent.

## Troubleshooting

- No devices: check BLE availability, internet/config access, and that the toy is advertising.
- Connection failure: keep the toy awake and close other apps using it.
- BLE write timeout: disconnect and reconnect before retrying; changing script frequency alone does not fix a failed BLE connection.
- Playback progress is timeline progress, not proof that the toy received a command.
- Generic macOS console builds are not a supported CoreBluetooth application setup.

See [API Reference](api-reference.md) and the [sample README](../README.md).
