using HoneyPlayBox.OpenSDK;
using HoneyPlayBox.OpenSDK.Enums;
using HoneyPlayBox.OpenSDK.Executors;
using HoneyPlayBox.OpenSDK.Models;
using HoneyPlayBox.OpenSDK.Transports;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("HoneyPlayBox .NET SDK Console Sample");
Console.WriteLine("====================================");

var transport = new HoneyPlayBoxToyTransport();

var connector = new ToyConnector(new ToyConnectorOptions
{
    AutoReconnect = true,
    ReconnectMaxAttempts = 3,
    ReconnectDelayMs = 500,
    ReconnectMaxDelayMs = 3000
});

connector.Discover += (_, toy) => Console.WriteLine($"[Discover] {toy.Name} ({toy.BluetoothName})");
connector.Connect += (_, state) => Console.WriteLine($"[Connect] {state.Name}: {state.Status}, connected={state.Connected}");
connector.Disconnect += (_, state) => Console.WriteLine($"[Disconnect] {state.Name}");
connector.Command += (_, command) =>
{
    // Decoded command summaries distinguish a successful write from clock progress
    // without exposing raw protocol frames in the sample console.
    var output = string.Join(", ", command.Commands.Select(action =>
        $"{action.Function}(motor={action.Motor}, value={action.Value ?? action.Strength}, duration={action.DurationMs ?? action.Duration}ms)"));
    Console.WriteLine($"[Command] {command.Name}: status={command.Status ?? "sent"}, {output}");
};
connector.QueueChange += (_, change) =>
{
    var commandText = string.IsNullOrWhiteSpace(change.CommandId) ? string.Empty : $", commandId={change.CommandId}";
    Console.WriteLine($"[Queue] {change.Name}: action={change.Action}{commandText}, queue={change.Queue.Count}");
};
connector.FunscriptStateChanged += (_, status) =>
{
    if (status.Reason == "progress")
    {
        Console.WriteLine(
            $"[Funscript] {status.Name}: {status.State}, {status.PositionMs:F0}/{status.DurationMs:F0}ms, " +
            $"rate={status.PlaybackRate:F2}x, progress={status.Progress:P0}");
        return;
    }

    var message = string.IsNullOrWhiteSpace(status.Message) ? string.Empty : $", message={status.Message}";
    Console.WriteLine($"[Funscript] {status.Name}: {status.State}, reason={status.Reason}{message}");
};
connector.Error += (_, error) => Console.WriteLine($"[Error] {error.Name}: {error.Action} - {error.Message}");
connector.Feedback += (_, feedback) =>
{
    if (feedback.Kind == "battery")
    {
        Console.WriteLine($"[Battery] {feedback.Name}: {feedback.Value}%");
        return;
    }

    if (feedback.Kind == DeviceNotificationType.Pressure.ToString())
    {
        Console.WriteLine($"[Pressure] {feedback.Name}: {feedback.Value}");
        return;
    }

    // Unknown notification kind can still carry a meaningful protocol response code.
    Console.WriteLine($"[Feedback] {feedback.Name}: kind={feedback.Kind}, value={feedback.Value}, unit={feedback.Unit}, response={feedback.ResponseStatus}");
};

var support = await HoneyPlayBoxToyTransport.GetSupportStateAsync();
Console.WriteLine($"Bluetooth state: supported={support.Supported}, available={support.Available}");

while (true)
{
    Console.WriteLine();
    Console.WriteLine("Main menu");
    Console.WriteLine("1. Search and connect toys");
    Console.WriteLine("2. Control connected toys");
    Console.WriteLine("3. Read battery");
    Console.WriteLine("4. Disconnect toy");
    Console.WriteLine("0. Exit");

    switch (await ReadCommandAsync("Enter command: "))
    {
        case "1":
            await SearchAndConnectAsync(connector, support.Available);
            break;

        case "2":
            await RunToyMenuAsync(connector);
            break;

        case "3":
            await SelectConnectedToyAsync(connector, "battery");
            break;

        case "4":
            await SelectConnectedToyAsync(connector, "disconnect");
            break;

        case "0":
            await DisconnectAllAsync(connector);
            return;

        default:
            Console.WriteLine("Unknown command.");
            break;
    }
}

static async Task SearchAndConnectAsync(ToyConnector connector, bool bluetoothAvailable)
{
    if (!bluetoothAvailable)
    {
        Console.WriteLine("Bluetooth is not available in this runtime.");
        return;
    }

    Console.WriteLine("Searching toys...");
    IReadOnlyList<ToyInfo> toys;
    try
    {
        toys = await connector.DiscoverDevicesAsync();
    }
    catch (Exception error)
    {
        Console.WriteLine($"Search failed: {error.Message}");
        return;
    }

    if (toys.Count == 0)
    {
        Console.WriteLine("No toys matched product config.");
        return;
    }

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("Choose toys to connect");
        for (var index = 0; index < toys.Count; index += 1)
        {
            var toy = toys[index];
            var connected = connector.GetToy(toy.ToyId) is null ? string.Empty : " (connected)";
            Console.WriteLine($"{index + 1}. {toy.Name}{connected}");
        }
        Console.WriteLine("a. Connect all");
        Console.WriteLine("0. Back");

        var command = await ReadCommandAsync("Enter number: ");
        if (command == "0")
        {
            return;
        }

        if (command.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var toy in toys)
            {
                await ConnectToyAsync(connector, toy);
            }
            return;
        }

        if (!int.TryParse(command, out var selectedIndex) || selectedIndex < 1 || selectedIndex > toys.Count)
        {
            Console.WriteLine("Invalid selection.");
            continue;
        }

        await ConnectToyAsync(connector, toys[selectedIndex - 1]);
    }
}

static async Task ConnectToyAsync(ToyConnector connector, ToyInfo toyInfo)
{
    if (connector.GetToy(toyInfo.ToyId) is not null)
    {
        Console.WriteLine($"Already connected: {toyInfo.Name}");
        return;
    }

    try
    {
        var toy = await connector.ConnectDeviceAsync(toyInfo.ToyId);
        var battery = toy.BatteryLevel is null ? "unknown" : $"{toy.BatteryLevel}%";
        Console.WriteLine($"Connected: {toy.Name}, battery={battery}");
    }
    catch (Exception error)
    {
        Console.WriteLine($"Connect failed: {error.Message}");
    }
}

static async Task SelectConnectedToyAsync(ToyConnector connector, string action)
{
    while (true)
    {
        var toys = connector.GetConnectedToys();
        if (toys.Count == 0)
        {
            Console.WriteLine("No connected toys.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Connected toys");
        for (var index = 0; index < toys.Count; index += 1)
        {
            var toy = toys[index];
            var battery = toy.BatteryLevel is null ? "unknown" : $"{toy.BatteryLevel}%";
            Console.WriteLine($"{index + 1}. {toy.Name}, battery={battery}");
        }
        Console.WriteLine("0. Back");

        var command = await ReadCommandAsync("Enter toy number: ");
        if (command == "0")
        {
            return;
        }

        if (!int.TryParse(command, out var selectedIndex) || selectedIndex < 1 || selectedIndex > toys.Count)
        {
            Console.WriteLine("Invalid selection.");
            continue;
        }

        var selectedToy = toys[selectedIndex - 1];
        try
        {
            switch (action)
            {
                case "battery":
                    var battery = await connector.Immediate.ReadBatteryAsync(selectedToy.ToyId);
                    Console.WriteLine($"Battery: {selectedToy.Name}: {(battery is null ? "unknown" : $"{battery}%")}");
                    break;
                case "disconnect":
                    await connector.Immediate.DisconnectAsync(selectedToy.ToyId);
                    break;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Command failed: {error.Message}");
        }
    }
}

static async Task RunToyMenuAsync(ToyConnector connector)
{
    Toy? toy = null;
    // Default to one connected target without a mandatory device-selection screen.
    // Navigation and target changes never send commands or interrupt active playback.
    while (true)
    {
        var toys = connector.GetConnectedToys();
        if (toys.Count == 0)
        {
            Console.WriteLine("No connected toys.");
            return;
        }
        toy = toys.FirstOrDefault(item => item.ToyId == toy?.ToyId) ?? toys[0];
        Console.WriteLine();
        Console.WriteLine($"Toy: {toy.Name}");
        Console.WriteLine("Choose control mode");
        Console.WriteLine("1. Immediate");
        Console.WriteLine("2. Funscript");
        Console.WriteLine("3. Queue");
        if (toys.Count > 1)
        {
            Console.WriteLine("t. Switch target toy");
        }
        Console.WriteLine("0. Back");

        switch (await ReadCommandAsync("Enter command: "))
        {
            case "1":
                await RunImmediateMenuAsync(connector, toy);
                break;
            case "2":
                await RunFunscriptMenuAsync(connector.Funscript, toy);
                break;
            case "3":
                await RunQueueMenuAsync(connector.Queue, toy);
                break;
            case "t":
                if (toys.Count == 1)
                {
                    Console.WriteLine("Only one toy is connected.");
                    break;
                }
                for (var index = 0; index < toys.Count; index++)
                {
                    Console.WriteLine($"{index + 1}. {toys[index].Name}");
                }
                Console.WriteLine("0. Back");
                var target = await ReadCommandAsync("Enter target number: ");
                if (target == "0")
                {
                    break;
                }
                if (int.TryParse(target, out var selectedIndex) && selectedIndex >= 1 && selectedIndex <= toys.Count)
                {
                    toy = toys[selectedIndex - 1];
                }
                else
                {
                    Console.WriteLine("Invalid selection.");
                }
                break;
            case "0":
                return;
            default:
                Console.WriteLine("Unknown command.");
                break;
        }
    }
}

static async Task RunImmediateMenuAsync(ToyConnector connector, Toy toy)
{
    while (connector.GetToy(toy.ToyId) is not null)
    {
        var supportsConstrict = toy.Features.Contains(ToyFunctions.Constrict);
        var positionFunction = GetPositionFunction(toy);

        Console.WriteLine();
        Console.WriteLine($"Immediate: {toy.Name}");
        if (toy.Features.Contains(ToyFunctions.Vibrate))
        {
            Console.WriteLine("1. Vibrate 50 for 1s");
            Console.WriteLine("2. Vibrate 50 for 10s");
            Console.WriteLine("3. Vibrate selected motor 50 for 3s");
        }
        if (supportsConstrict)
        {
            Console.WriteLine("4. Constrict 50 for 1s");
        }
        if (positionFunction is not null)
        {
            Console.WriteLine("5. Position center=50 amplitude=30 speed=5 for 1s");
        }
        Console.WriteLine("6. Stop immediate output");
        Console.WriteLine("p. Read pressure mode");
        Console.WriteLine("e. Enable pressure mode");
        Console.WriteLine("x. Disable pressure mode");
        Console.WriteLine("0. Back");

        try
        {
            switch (await ReadCommandAsync("Enter command: "))
            {
                case "1":
                    RequireFunction(toy, ToyFunctions.Vibrate);
                    await connector.Immediate.ExecuteAsync(toy.ToyId, new VibrationAction { Function = ToyFunctions.Vibrate, Value = 50, DurationMs = 1000 });
                    break;

                case "2":
                    RequireFunction(toy, ToyFunctions.Vibrate);
                    await connector.Immediate.ExecuteAsync(toy.ToyId, new VibrationAction { Function = ToyFunctions.Vibrate, Value = 50, DurationMs = 10000 });
                    break;

                case "3":
                    RequireFunction(toy, ToyFunctions.Vibrate);
                    await VibrateSelectedMotorAsync(connector.Immediate, toy);
                    break;

                case "4":
                    if (!supportsConstrict)
                    {
                        Console.WriteLine("This toy does not support constrict.");
                        break;
                    }
                    await connector.Immediate.ExecuteAsync(toy.ToyId, new VibrationAction { Function = ToyFunctions.Constrict, Value = 50, DurationMs = 1000 });
                    break;

                case "5":
                    if (positionFunction is null)
                    {
                        Console.WriteLine("This toy does not support position control.");
                        break;
                    }
                    await connector.Immediate.ExecuteAsync(toy.ToyId, new VibrationAction
                    {
                        Function = positionFunction.Value,
                        CenterPosition = 50,
                        Amplitude = 30,
                        Speed = 5,
                        DurationMs = 1000
                    });
                    break;

                case "6":
                    await connector.Immediate.StopAsync(toy.ToyId);
                    break;

                case "p":
                    Console.WriteLine($"Pressure mode: {(await toy.ReadPressureModeAsync() ? "enabled" : "disabled")}");
                    break;

                case "e":
                    Console.WriteLine($"Set pressure mode: {(await toy.SetPressureModeAsync(true)).ResponseStatus}");
                    break;

                case "x":
                    Console.WriteLine($"Set pressure mode: {(await toy.SetPressureModeAsync(false)).ResponseStatus}");
                    break;

                case "0":
                    return;

                default:
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Command failed: {error.Message}");
        }
    }
}

static async Task RunQueueMenuAsync(QueueExecutor queue, Toy toy)
{
    while (queue.GetToy(toy.ToyId) is not null)
    {
        var positionFunction = GetPositionFunction(toy);
        Console.WriteLine();
        Console.WriteLine($"Queue: {toy.Name}");
        if (toy.Features.Contains(ToyFunctions.Vibrate))
        {
            Console.WriteLine("1. Enqueue vibration 50 for 1s");
            Console.WriteLine("2. Enqueue selected motor vibration 50 for 3s");
        }
        if (toy.Features.Contains(ToyFunctions.Constrict))
        {
            Console.WriteLine("3. Enqueue constrict 50 for 1s");
        }
        if (positionFunction is not null)
        {
            Console.WriteLine("4. Enqueue position center=50 amplitude=30 speed=5 for 1s");
        }
        Console.WriteLine("5. Show queue");
        Console.WriteLine("6. Remove queued command");
        Console.WriteLine("7. Clear queue");
        Console.WriteLine("0. Back");

        try
        {
            switch (await ReadCommandAsync("Enter command: "))
            {
                case "1":
                    RequireFunction(toy, ToyFunctions.Vibrate);
                    await queue.ExecuteAsync(toy.ToyId, new VibrationAction
                    {
                        Function = ToyFunctions.Vibrate,
                        Value = 50,
                        DurationMs = 1000
                    });
                    break;
                case "2":
                    RequireFunction(toy, ToyFunctions.Vibrate);
                    var motor = await ReadMotorAsync(toy);
                    await queue.ExecuteAsync(toy.ToyId, new VibrationAction
                    {
                        Function = ToyFunctions.Vibrate,
                        Motor = motor,
                        Value = 50,
                        DurationMs = 3000
                    });
                    break;
                case "3":
                    RequireFunction(toy, ToyFunctions.Constrict);
                    await queue.ExecuteAsync(toy.ToyId, new VibrationAction
                    {
                        Function = ToyFunctions.Constrict,
                        Value = 50,
                        DurationMs = 1000
                    });
                    break;
                case "4":
                    if (positionFunction is null)
                    {
                        throw new InvalidOperationException("This toy does not support position control.");
                    }
                    await queue.ExecuteAsync(toy.ToyId, new VibrationAction
                    {
                        Function = positionFunction.Value,
                        CenterPosition = 50,
                        Amplitude = 30,
                        Speed = 5,
                        DurationMs = 1000
                    });
                    break;
                case "5":
                    PrintQueue(queue, toy);
                    break;
                case "6":
                    await RemoveQueuedCommandAsync(queue, toy);
                    break;
                case "7":
                    Console.WriteLine($"Removed queue commands: {queue.Clear(toy.ToyId)}");
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Queue command failed: {error.Message}");
        }
    }
}

static async Task RunFunscriptMenuAsync(FunscriptExecutor funscript, Toy toy)
{
    while (funscript.GetToy(toy.ToyId) is not null)
    {
        Console.WriteLine();
        Console.WriteLine($"Funscript: {toy.Name}");
        Console.WriteLine("1. Load .funscript file or URL");
        Console.WriteLine("2. Start");
        Console.WriteLine("3. Pause");
        Console.WriteLine("4. Resume");
        Console.WriteLine("5. Stop");
        Console.WriteLine("6. Restart");
        Console.WriteLine("7. Seek to time");
        Console.WriteLine("8. Jump relative time");
        Console.WriteLine("9. Set playback rate");
        Console.WriteLine("t. Synchronize time");
        Console.WriteLine("s. Show status");
        Console.WriteLine("0. Back");

        try
        {
            switch (await ReadCommandAsync("Enter command: "))
            {
                case "1":
                    var source = (await ReadCommandAsync("Funscript file path or HTTP/HTTPS URL: ")).Trim('"');
                    var isOnline = Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
                    var json = isOnline ? source : await File.ReadAllTextAsync(source);
                    var content = await funscript.LoadAsync(toy.ToyId, json);
                    Console.WriteLine($"Loaded script content: {content.Length} characters");
                    PrintFunscriptStatus(funscript.GetStatus(toy.ToyId));
                    break;
                case "2":
                    PrintFunscriptStatus(await funscript.StartAsync(toy.ToyId));
                    break;
                case "3":
                    PrintFunscriptStatus(await funscript.PauseAsync(toy.ToyId));
                    break;
                case "4":
                    PrintFunscriptStatus(await funscript.ResumeAsync(toy.ToyId));
                    break;
                case "5":
                    PrintFunscriptStatus(await funscript.StopAsync(toy.ToyId));
                    break;
                case "6":
                    PrintFunscriptStatus(await funscript.RestartAsync(toy.ToyId));
                    break;
                case "7":
                    PrintFunscriptStatus(await funscript.SeekAsync(toy.ToyId, await ReadNumberAsync("Target time (ms): ")));
                    break;
                case "8":
                    PrintFunscriptStatus(await funscript.JumpAsync(toy.ToyId, await ReadNumberAsync("Offset (ms, negative allowed): ")));
                    break;
                case "9":
                    PrintFunscriptStatus(await funscript.SetPlaybackRateAsync(toy.ToyId, await ReadNumberAsync("Playback rate (0.1-4): ")));
                    break;
                case "t":
                    PrintFunscriptStatus(await funscript.SynchronizeTimeAsync(toy.ToyId, await ReadNumberAsync("Authoritative time (ms): ")));
                    break;
                case "s":
                    PrintFunscriptStatus(funscript.GetStatus(toy.ToyId));
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Funscript command failed: {error.Message}");
        }
    }
}

static async Task<double> ReadNumberAsync(string prompt)
{
    if (!double.TryParse(await ReadCommandAsync(prompt), out var value) || !double.IsFinite(value))
    {
        throw new InvalidOperationException("Enter a valid number.");
    }
    return value;
}

static void PrintFunscriptStatus(FunscriptPlaybackStatus status)
{
    Console.WriteLine(
        $"State={status.State}, position={status.PositionMs:F0}ms, duration={status.DurationMs:F0}ms, " +
        $"rate={status.PlaybackRate:F2}x, progress={status.Progress:P0}");
    foreach (var track in status.Tracks)
    {
        Console.WriteLine($"{track.TrackId} ({track.Function}): " +
            (track.Matched ? "Matched" : $"Skipped - {track.Reason}"));
    }
}

// Use the configured capability list for both menu visibility and command validation.
static void RequireFunction(Toy toy, ToyFunctions function)
{
    if (!toy.Features.Contains(function))
    {
        throw new InvalidOperationException($"This toy does not support {function}.");
    }
}

static ToyFunctions? GetPositionFunction(Toy toy) =>
    toy.Features.Contains(ToyFunctions.Position) ? ToyFunctions.Position :
    toy.Features.Contains(ToyFunctions.HwPositionWithDuration) ? ToyFunctions.HwPositionWithDuration : null;

static async Task<int> ReadMotorAsync(Toy toy)
{
    if (!int.TryParse(await ReadCommandAsync($"Enter motor number (1-{toy.MotorCount}): "), out var motor) ||
        motor < 1 || motor > toy.MotorCount)
    {
        throw new InvalidOperationException("Invalid motor number.");
    }
    return motor;
}

static async Task VibrateSelectedMotorAsync(ImmediateExecutor immediate, Toy toy)
{
    var motor = await ReadMotorAsync(toy);

    await immediate.ExecuteAsync(toy.ToyId, new VibrationAction
    {
        Function = ToyFunctions.Vibrate,
        Motor = motor,
        Value = 50,
        DurationMs = 3000
    });
}

static async Task RemoveQueuedCommandAsync(QueueExecutor queue, Toy toy)
{
    PrintQueue(queue, toy);
    var commandId = await ReadCommandAsync("Enter command id: ");
    if (string.IsNullOrWhiteSpace(commandId))
    {
        return;
    }

    Console.WriteLine(queue.Remove(toy.ToyId, commandId) ? "Queued command removed." : "Command cannot be removed.");
}

static void PrintQueue(QueueExecutor executor, Toy toy)
{
    var queue = executor.GetItems(toy.ToyId);
    if (queue.Count == 0)
    {
        Console.WriteLine("Command queue is empty.");
        return;
    }

    foreach (var item in queue)
    {
        Console.WriteLine($"{item.Position}. {item.CommandId}, status={item.Status}, count={item.CommandCount}");
    }
}

static async Task DisconnectAllAsync(ToyConnector connector)
{
    foreach (var toy in connector.GetConnectedToys())
    {
        await toy.DisconnectAsync();
    }
}

static async Task<string> ReadCommandAsync(string prompt)
{
    Console.Write(prompt);
    // Console input is synchronous even when wrapped by some TextReader async APIs.
    // Keep the blocking read off the menu/WinRT continuation thread and thread pool.
    var line = await Task.Factory.StartNew(Console.ReadLine, CancellationToken.None,
        TaskCreationOptions.LongRunning, TaskScheduler.Default);
    // End-of-input follows the same exit/back path as entering zero, not a busy loop.
    return line?.Trim() ?? "0";
}
