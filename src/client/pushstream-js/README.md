# PushStream

[![NuGet - Core](https://img.shields.io/nuget/v/PushStream.Core?label=PushStream.Core)](https://www.nuget.org/packages/PushStream.Core)
[![NuGet - AspNetCore](https://img.shields.io/nuget/v/PushStream.AspNetCore?label=PushStream.AspNetCore)](https://www.nuget.org/packages/PushStream.AspNetCore)
[![npm](https://img.shields.io/npm/v/pushstream-client)](https://www.npmjs.com/package/pushstream-client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Real-time server push without the complexity.**

PushStream is an opinionated abstraction over Server-Sent Events (SSE) that makes implementing real-time server-to-client updates simple, safe, and maintainable.

---

## Why PushStream?

Most real-time features only need **one-way communication** — the server pushing updates to clients:

- Task progress notifications
- Background job status
- AI/ML processing events
- Live activity feeds

Yet developers often reach for WebSockets or SignalR, introducing unnecessary complexity for simple push scenarios.

**PushStream gives you real-time behavior without real-time complexity.**

---

## Installation

### Server (.NET)

```bash
dotnet add package PushStream.AspNetCore
```

### Client (npm)

```bash
npm install pushstream-client
```

### Client (CDN)

```html
<script src="https://unpkg.com/pushstream-client/dist/pushstream.min.js"></script>
<script>
  const client = new PushStream.EventClient('/events');
</script>
```

---

## Quick Start

### Server (ASP.NET Core)

```csharp
// 1. Register PushStream services
builder.Services.AddPushStream();

// 2. Map an SSE endpoint
app.MapEventStream("/events");

// 3. Publish events from anywhere
public class TaskService
{
    private readonly IEventPublisher _publisher;

    public TaskService(IEventPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task ProcessTaskAsync(string taskId)
    {
        await _publisher.PublishAsync("task.started", new { taskId });
        
        // ... do work ...
        
        await _publisher.PublishAsync("task.progress", new { taskId, percentage = 50 });
        
        // ... complete work ...
        
        await _publisher.PublishAsync("task.completed", new { taskId, result = "Success" });
    }
}
```

### Client (JavaScript/TypeScript)

```javascript
import { EventClient } from 'pushstream-client';

const client = new EventClient('/events');

client.on('task.started', (data) => {
  console.log(`Task ${data.taskId} started`);
});

client.on('task.progress', (data) => {
  console.log(`Progress: ${data.percentage}%`);
});

client.on('task.completed', (data) => {
  console.log(`Completed: ${data.result}`);
});

client.connect();
```

That's it. No connection management. No reconnection logic. No heartbeat handling.

---

## Features

| Feature | Description |
|---------|-------------|
| **Simple API** | Event-centric design that matches how you think |
| **Auto-reconnect** | Client handles connection drops gracefully |
| **Heartbeats** | Built-in keep-alive, no configuration needed |
| **Named Events** | Predictable `domain.action` event naming |
| **Type Safety** | Strongly typed events on the server |
| **Framework Agnostic** | Core logic works anywhere, adapters for popular frameworks |

---

## When to Use PushStream

✅ **Use PushStream when:**
- Updates flow only from server to client
- You need instant feedback for long-running operations
- You want real-time without managing WebSocket complexity

❌ **Don't use PushStream when:**
- You need bi-directional messaging (use SignalR/WebSockets)
- You're building a chat application with client-to-client messaging
- You need request-response patterns over the same connection

---

## Documentation

| Document | Description |
|----------|-------------|
| [Problem Statement](docs/01-problem-statement.md) | Why this project exists |
| [Why SSE?](docs/02-why-see.md) | Technical justification for SSE over alternatives |
| [Architecture](docs/03-architecture.md) | System design and component overview |
| [Design Decisions](docs/04-design-decision.md) | Key architectural choices and rationale |
| [API Design](docs/05-api-design.md) | Public API reference |
| [Getting Started](docs/07-getting-started.md) | Step-by-step setup guide |
| [Security & Auth](docs/08-security-and-auth.md) | Authentication considerations |
| [Future Roadmap](docs/06-future-roadmap.md) | What's coming next |

---

## Project Structure

```
PushStream/
├── src/
│   ├── server/
│   │   ├── PushStream.Core/         # Core abstractions (NuGet)
│   │   ├── PushStream.AspNetCore/   # ASP.NET Core integration (NuGet)
│   │   └── PushStream.DemoApi/      # Demo application
│   └── client/
│       └── pushstream-js/           # JavaScript client (npm: pushstream-client)
├── tests/
│   ├── PushStream.Core.Tests/
│   └── PushStream.AspNetCore.Tests/
├── docs/                            # Documentation
├── specs/                           # Specifications
└── README.md
```

---

## License

MIT

---

## Contributing

Contributions are welcome! Please read the documentation first to understand the design philosophy and non-goals of the project.