using System.Reflection;
using ChatRoom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Runtime;
using Spectre.Console;

using var host = new HostBuilder()
    .UseOrleansClient(clientBuilder =>
    {
        clientBuilder.UseLocalhostClustering()
            .AddMemoryStreams("chat");
    })
    .Build();

PrintUsage();

var client = host.Services.GetRequiredService<IClusterClient>();

ClientContext context = new(client);
await StartAsync(host);
context = context with
{
    UserName = AnsiConsole.Ask<string>("Введите ваше [aqua]имя[/]")
};
context = await JoinChannel(context, "1");
context = await GetStart(context, "1");
context = await GetNumber(context, "1");
await ProcessLoopAsync(context);

await StopAsync(host);

static Task StartAsync(IHost host) =>
    AnsiConsole.Status().StartAsync("Подключение к серверу", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Dots);
        ctx.Status = "Подключение...";

        await host.StartAsync();

        ctx.Status = "Подключено!";
    });

static async Task ProcessLoopAsync(ClientContext context)
{
    string? input = null;
    do
    {
        input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }

        if (input.StartsWith("/exit") &&
            AnsiConsole.Confirm("Do you really want to exit?"))
        {
            break;
        }

        var firstTwoCharacters = input.Length >= 2 ? input[..2] : string.Empty;
        
        if (firstTwoCharacters switch
        {
            "/j" => JoinChannel(context, input.Replace("/j", "").Trim()),
            "/l" => LeaveChannel(context),
            _ => null
        } is Task<ClientContext> cxtTask)
        {
            context = await cxtTask;
            continue;
        }

        if (firstTwoCharacters switch
        {
            "/m" => ShowChannelMembers(context),
            _ => null
        } is Task task)
        {
            await task;
            continue;
        }

        if (context.IsConnectedToChannel)
        {
            await SendMessage(context, input);
        }
    } while (input is not "/exit");
}

static Task StopAsync(IHost host) =>
    AnsiConsole.Status().StartAsync("Disconnecting...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Dots);
        await host.StopAsync();
    });

static void PrintUsage()
{
    AnsiConsole.WriteLine();

    var table = new Table()
    {
        Border = TableBorder.None,
        Expand = true,
    }.HideHeaders();

    var header = new FigletText("Test task")
    {
        Color = Color.Aqua
    };
    var header2 = new FigletText("Multicast")
    {
        Color = Color.Fuchsia
    };

    var markup = new Markup(
       "Введите:\n[bold fuchsia]/m[/] чтобы увидеть список [underline green]игроков[/] и их [underline green]очки[/]\n"
        + "[bold fuchsia]/l[/] чтобы [underline green]выйти[/] из текущей комнаты\n"
        + "[bold fuchsia]/j[/] чтобы [underline green]войти[/] в новую комнату\n");
    table.AddColumn(new TableColumn("One"));

    var rightTable = new Table()
        .HideHeaders()
        .Border(TableBorder.None)
        .AddColumn(new TableColumn("Content"));

    rightTable.AddRow(header)
        .AddRow(header2)
        .AddEmptyRow()
        .AddEmptyRow()
        .AddRow(markup);
    table.AddRow(rightTable);

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

static async Task ShowChannelMembers(ClientContext context)
{
    var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);

    if (!context.IsConnectedToChannel)
    {
        AnsiConsole.MarkupLine("[bold red]Вы не подключены к комнате[/]");
        return;
    }

    var members = await room.GetMembers();

    //AnsiConsole.Write(new Rule($"Members for '{context.CurrentChannel}'")
    AnsiConsole.Write(new Rule($"Игроки в комнате")
    {
        Justification = Justify.Center,
        Style = Style.Parse("darkgreen")
    });

    foreach (var member in members)
    {
        AnsiConsole.MarkupLine($"[bold yellow]Имя: {member.Nickname}, Очки: {member.Points}[/]");
    }

    AnsiConsole.Write(new Rule()
    {
        Justification = Justify.Center,
        Style = Style.Parse("darkgreen")
    });
}

static async Task ShowCurrentChannelHistory(ClientContext context)
{
    var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);

    if (!context.IsConnectedToChannel)
    {
        AnsiConsole.MarkupLine("[bold red]You are not connected to any channel[/]");
        return;
    }

    var history = await room.ReadHistory(1_000);

    AnsiConsole.Write(new Rule($"History for '{context.CurrentChannel}'")
    {
        Justification = Justify.Center,
        Style = Style.Parse("darkgreen")
    });

    foreach (var chatMsg in history)
    {
        AnsiConsole.MarkupLine("[[[dim]{0}[/]]] [bold yellow]{1}:[/] {2}",
            chatMsg.Created.LocalDateTime, chatMsg.Author, chatMsg.Text);
    }

    AnsiConsole.Write(new Rule()
    {
        Justification = Justify.Center,
        Style = Style.Parse("darkgreen")
    });
}

static async Task SendMessage(
    ClientContext context,
    string messageText)
{
    var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);
    await room.Message(new ChatMsg(context.UserName, messageText));
}

static async Task<ClientContext> JoinChannel(
    ClientContext context,
    string channelName)
{
    if (context.CurrentChannel is not null &&
        !string.Equals(context.CurrentChannel, channelName, StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine(
            "[bold olive]Вышел с канала [/]{0}[bold olive] после входа [/]{1}",
            context.CurrentChannel, channelName);

        await LeaveChannel(context);
    }

    AnsiConsole.MarkupLine("[bold aqua]Вход в комнату[/]");
    context = context with { CurrentChannel = channelName };
    await AnsiConsole.Status().StartAsync("Вход в комнату...", async ctx =>
    {
        var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);
        await room.Join(context.UserName!);
        
        var streamId = StreamId.Create("ChatRoom", context.CurrentChannel!);
        var stream =
            context.Client
                .GetStreamProvider("chat")
                .GetStream<ChatMsg>(streamId);

        await stream.SubscribeAsync(new StreamObserver(channelName));
    });
    AnsiConsole.MarkupLine("[bold aqua]Вы присоединились к комнате[/]");
    return context;
}
static async Task<ClientContext> GetStart(ClientContext context, string channelName)
{
    bool start = false;
    if (context.CurrentChannel is not null &&
        !string.Equals(context.CurrentChannel, channelName, StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine(
            "[bold olive]Вышел из комнаты [/]{0}[bold olive] после входа [/]{1}",
            context.CurrentChannel, channelName);

        await LeaveChannel(context);
    }

    context = context with { CurrentChannel = channelName };

    var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);

    await AnsiConsole.Status().StartAsync("Ожидания других игроков для старта игры...", async ctx =>
    {
        while (!room.Start().Result)
            await Task.Yield();
    });

    return context;
}

static async Task<ClientContext> GetNumber(ClientContext context, string channelName)
{
    bool start = false;
    if (context.CurrentChannel is not null &&
        !string.Equals(context.CurrentChannel, channelName, StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine(
            "[bold olive]Вышел с канала [/]{0}[bold olive] после входа [/]{1}",
            context.CurrentChannel, channelName);

        await LeaveChannel(context);
    }

    context = context with { CurrentChannel = channelName };

    return context;
}

static async Task<ClientContext> LeaveChannel(ClientContext context)
{
    if (!context.IsConnectedToChannel)
    {
        AnsiConsole.MarkupLine("[bold red]You are not connected to any channel[/]");
        return context;
    }

    AnsiConsole.MarkupLine(
        "[bold olive]Вышел из комнаты [/]{0}",
        context.CurrentChannel!);

    await AnsiConsole.Status().StartAsync("Выход с комнаты...", async ctx =>
    {
        var room = context.Client.GetGrain<IChannelGrain>(context.CurrentChannel);
        await room.Leave(context.UserName!);
        var streamId = StreamId.Create("ChatRoom", context.CurrentChannel!);
        var stream =
            context.Client
                .GetStreamProvider("chat")
                .GetStream<ChatMsg>(streamId);

        var subscriptionHandles = await stream.GetAllSubscriptionHandles();
        foreach (var handle in subscriptionHandles)
        {
            await handle.UnsubscribeAsync();
        }
    });

    AnsiConsole.MarkupLine("[bold olive]Вы вышли с комнаты [/]{0}", context.CurrentChannel!);

    return context with { CurrentChannel = null };
}
