# API Design

This document describes the public API exposed by the library and the design
principles behind it.

The API is intentionally small, event-centric, and easy to reason about,
allowing developers to adopt real-time updates with minimal friction.

---

## Design Goals

The API is designed to:

- Be intuitive for backend and frontend developers
- Hide SSE-specific implementation details
- Encourage event-driven patterns
- Require minimal configuration
- Be safe by default

---

## Core Concepts

The API is built around four core concepts:

1. **Event Stream**
2. **Event Publisher**
3. **Subscription**
4. **Client Listener**

Each concept maps directly to how developers think about real-time updates.

---

## Server-Side API

### EventStream

Represents an SSE stream that clients can subscribe to.

```csharp
public interface IEventStream
{
    Task PublishAsync<TEvent>(string eventName, TEvent payload);
}
```

Responsibilities:
- Serializing events
- Broadcasting to active subscribers
- Maintaining event order

## Event Publisher

Encapsulates publishing logic and is used by business services.

```csharp
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(string eventName, TEvent payload);
}
```

## SSE Endpoint Registration

The library exposes a simple way to register an SSE endpoint.

```csharp
app.MapEventStream("/events");
```

This endpoint:
- Opens an SSE connection
- Registers the client
- Starts streaming events

---

# Client-Side API

## EventClient

A lightweight wrapper over EventSource.

```javascript
const client = new EventClient("/events");
```

## Subscribing to Events

```javascript
client.on("task.progress", (data) => {
  console.log("Progress:", data.percentage);
});

client.on("task.completed", (data) => {
  console.log("Completed:", data.result);
});
```

Developers subscribe to events, not transport mechanisms.

## Connection Lifecycle

```javascript
client.connect();
client.disconnect();
```

The client automatically:
- Reconnects on network failure
- Resubscribes to events
- Handles errors gracefully

---

# Event Naming Conventions

Events follow a predictable naming structure:

```
<domain>.<action>
```

Examples:
- task.started
- task.progress
- task.completed
- notification.received

This encourages consistency and discoverability.

---

## Payload Design

Payloads are:
- JSON-serializable
- Strongly typed on the server
- Loosely typed on the client

```json
{
  "taskId": "123",
  "status": "IN_PROGRESS",
  "percentage": 60
}
```

---

## Error Handling

Errors are surfaced as events:

```javascript
client.on("stream.error", (error) => {
  console.error("SSE Error:", error);
});
```

This keeps error handling consistent with the event-driven model.
