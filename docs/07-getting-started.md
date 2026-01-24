# Getting Started

This guide walks you through setting up PushStream in your application from
scratch. By the end, you'll have a working real-time update system.

---

## Prerequisites

- .NET 8.0+ (for server)
- Node.js 18+ (for client, optional)
- Basic understanding of ASP.NET Core

---

## Installation

### Server (NuGet)

```bash
dotnet add package PushStream.AspNetCore
```

### Client (npm)

```bash
npm install pushstream-client
```

---

## Step 1: Configure the Server

### Register Services

In your `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add PushStream services
builder.Services.AddPushStream();

var app = builder.Build();

// Map the SSE endpoint
app.MapEventStream("/events");

app.Run();
```

### Configuration Options (Optional)

```csharp
builder.Services.AddPushStream(options =>
{
    options.HeartbeatInterval = TimeSpan.FromSeconds(30);
    options.RetryInterval = TimeSpan.FromSeconds(3);
});
```

---

## Step 2: Publish Events

Inject `IEventPublisher` into any service or controller:

```csharp
public class OrderService
{
    private readonly IEventPublisher _publisher;

    public OrderService(IEventPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task ProcessOrderAsync(int orderId)
    {
        // Notify: order processing started
        await _publisher.PublishAsync("order.processing", new 
        { 
            orderId,
            status = "Processing"
        });

        // Simulate work
        await Task.Delay(2000);

        // Notify: order completed
        await _publisher.PublishAsync("order.completed", new 
        { 
            orderId,
            status = "Completed",
            timestamp = DateTime.UtcNow
        });
    }
}
```

---

## Step 3: Subscribe from the Client

### Using the JavaScript Client

```javascript
import { EventClient } from 'pushstream-client';

// Create client instance
const client = new EventClient('/events');

// Subscribe to events
client.on('order.processing', (data) => {
    console.log(`Order ${data.orderId} is being processed...`);
    showLoadingSpinner();
});

client.on('order.completed', (data) => {
    console.log(`Order ${data.orderId} completed at ${data.timestamp}`);
    hideLoadingSpinner();
    showSuccessMessage();
});

// Handle errors
client.on('stream.error', (error) => {
    console.error('Connection error:', error);
});

// Connect
client.connect();
```

### Using Native EventSource (No Library)

If you prefer not to use the client library:

```javascript
const eventSource = new EventSource('/events');

eventSource.addEventListener('order.processing', (event) => {
    const data = JSON.parse(event.data);
    console.log('Processing:', data);
});

eventSource.addEventListener('order.completed', (event) => {
    const data = JSON.parse(event.data);
    console.log('Completed:', data);
});

eventSource.onerror = (error) => {
    console.error('SSE Error:', error);
};
```

---

## Step 4: Add User/Session Targeting (Optional)

To send events to specific users:

```csharp
// In your endpoint, identify the client
app.MapEventStream("/events", context =>
{
    // Extract user identifier from auth token, query string, etc.
    var userId = context.User.FindFirst("sub")?.Value;
    return userId ?? context.Connection.Id;
});
```

Publish to specific users:

```csharp
await _publisher.PublishToAsync(userId, "notification.new", new 
{ 
    message = "You have a new message!" 
});
```

---

## Complete Example

### Server (Program.cs)

```csharp
using Microsoft.AspNetCore.Builder;
using PushStream.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPushStream();
builder.Services.AddScoped<TaskService>();

var app = builder.Build();

app.MapEventStream("/events");

app.MapPost("/api/tasks/start", async (TaskService taskService) =>
{
    var taskId = Guid.NewGuid().ToString();
    _ = taskService.RunTaskAsync(taskId); // Fire and forget
    return Results.Ok(new { taskId });
});

app.Run();

public class TaskService
{
    private readonly IEventPublisher _publisher;

    public TaskService(IEventPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task RunTaskAsync(string taskId)
    {
        await _publisher.PublishAsync("task.started", new { taskId });

        for (int i = 10; i <= 100; i += 10)
        {
            await Task.Delay(500);
            await _publisher.PublishAsync("task.progress", new { taskId, percentage = i });
        }

        await _publisher.PublishAsync("task.completed", new { taskId, result = "Success" });
    }
}
```

### Client (index.html)

```html
<!DOCTYPE html>
<html>
<head>
    <title>PushStream Demo</title>
</head>
<body>
    <button id="startBtn">Start Task</button>
    <div id="progress"></div>
    <div id="status"></div>

    <script type="module">
        import { EventClient } from './pushstream.js';

        const client = new EventClient('/events');
        const progressEl = document.getElementById('progress');
        const statusEl = document.getElementById('status');

        client.on('task.started', (data) => {
            statusEl.textContent = `Task ${data.taskId} started...`;
        });

        client.on('task.progress', (data) => {
            progressEl.textContent = `Progress: ${data.percentage}%`;
        });

        client.on('task.completed', (data) => {
            statusEl.textContent = `Task completed: ${data.result}`;
        });

        client.connect();

        document.getElementById('startBtn').addEventListener('click', async () => {
            await fetch('/api/tasks/start', { method: 'POST' });
        });
    </script>
</body>
</html>
```

---

## Next Steps

- Read [Architecture](03-architecture.md) to understand how components interact
- Review [API Design](05-api-design.md) for full API reference
- Check [Security & Auth](08-security-and-auth.md) for authentication patterns