# Design Decisions

This document captures the key architectural and API design decisions made
during the development of this library, along with the reasoning behind them.

The goal is to make the system easy to understand, extend, and maintain,
while avoiding unnecessary complexity.

---

## 1. Abstraction Over Raw SSE

### Decision
Provide a high-level abstraction over native Server-Sent Events instead of
exposing raw SSE primitives.

### Rationale
While SSE itself is simple, using it correctly requires handling multiple
cross-cutting concerns:
- Connection lifecycle
- Reconnection behavior
- Event naming and parsing
- Error handling
- Heartbeats and timeouts

Abstracting these concerns:
- Reduces boilerplate
- Prevents subtle bugs
- Encourages consistent usage across applications

### Trade-off
A higher-level abstraction limits some low-level control, but the benefits of
safety and consistency outweigh this cost for most use cases.

---

## 2. Unidirectional Communication by Design

### Decision
The library strictly supports **server-to-client** communication only.

### Rationale
Most real-time application requirements involve:
- Notifications
- Status updates
- Background job progress

Supporting bi-directional messaging would:
- Increase API surface area
- Introduce state management complexity
- Blur the responsibility of the library

By enforcing unidirectionality, the library remains focused and predictable.

### Trade-off
Applications requiring two-way communication should use WebSockets or
SignalR instead.

---

## 3. Opinionated Defaults with Escape Hatches

### Decision
Ship with sensible defaults while allowing controlled customization.

### Defaults Include:
- Retry intervals
- Heartbeat frequency
- Event serialization format
- Connection timeout behavior

### Rationale
Most developers want real-time updates to “just work.”
Opinionated defaults reduce configuration overhead and cognitive load.

At the same time, extension points are provided for advanced use cases.

---

## 4. Event-Centric API Design

### Decision
Expose events as the primary unit of interaction rather than connections or
streams.

### Rationale
Developers think in terms of **events**, not transport mechanisms.

An event-centric API:
- Improves readability
- Encourages domain-driven design
- Decouples business logic from transport details

This also aligns naturally with frontend consumption patterns.

---

## 5. Decoupled Event Publishing

### Decision
Separate event publishing from SSE connection management.

### Rationale
Business logic should not be aware of:
- Active connections
- Streaming protocols
- Client subscription states

By introducing an event publisher abstraction:
- Services remain testable
- SSE becomes an implementation detail
- Future transports can be introduced without rewriting business logic

---

## 6. Minimal Public API Surface

### Decision
Expose a small, focused public API.

### Rationale
A minimal API:
- Reduces learning curve
- Prevents misuse
- Makes breaking changes less likely
- Improves long-term maintainability

Internal complexity is hidden behind stable interfaces.

---

## 7. Framework-Agnostic Core

### Decision
Keep the core logic framework-agnostic, with thin adapters for specific
platforms (e.g., ASP.NET, frontend frameworks).

### Rationale
This enables:
- Easier testing
- Better portability
- Clear separation between core logic and platform concerns

Adapters translate platform-specific details into the core abstractions.

---

## 8. Explicit Non-Goals

### Decision
Clearly define what the library will **not** do.

### Non-Goals Include:
- Full real-time frameworks
- Message brokers
- Client-to-server messaging
- Transport-level optimization beyond SSE

### Rationale
Clear non-goals prevent scope creep and keep the project focused.

## Repo Structure

```
PushStream/
├── src/
│   ├── server/
│   │   ├── PushStream.Core/        # Core abstractions
│   │   ├── PushStream.AspNetCore/  # [ASP.NET](http://asp.net/) integration
│   │   └── PushStream.DemoApi/     # Sample API
│   └── client/
│       └── pushstream-js/          # JS/TS client
├── samples/
│   └── TaskProgress/               # End-to-end example
├── tests/
├── docs/
└── [README.md](http://readme.md/)
```