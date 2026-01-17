# PushStream.AspNetCore

ASP.NET Core integration for PushStream - real-time server push without complexity.

## Installation

```bash
dotnet add package PushStream.AspNetCore
```

## Quick Start

```csharp
// Program.cs
using PushStream.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Register PushStream services
builder.Services.AddPushStream();

var app = builder.Build();

// 2. Map an SSE endpoint
app.MapEventStream("/events");

// 3. Publish events from anywhere
app.MapPost("/notify", async (IEventPublisher publisher) =>
{
    await publisher.PublishAsync("task.completed", new { taskId = 123 });
    return Results.Ok();
});

app.Run();
```

## Features

- **Simple API** - Event-centric design that matches how you think
- **Auto-heartbeats** - Built-in keep-alive, no configuration needed
- **Named Events** - Predictable `domain.action` event naming
- **Targeted Publishing** - Send events to specific clients
- **Type Safety** - Strongly typed events on the server

## Configuration

```csharp
builder.Services.AddPushStream(options =>
{
    options.HeartbeatInterval = TimeSpan.FromSeconds(30);
    options.ClientIdResolver = context => context.User?.Identity?.Name;
});
```

## Publishing Events

```csharp
// Broadcast to all connected clients
await publisher.PublishAsync("order.updated", new { orderId = 123, status = "Shipped" });

// Send to a specific client
await publisher.PublishToAsync("user-123", "notification", new { message = "Hello!" });
```

## Documentation

For full documentation, visit: https://github.com/dishumavi/PushStream

## License

MIT
