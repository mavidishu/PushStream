# MVP Scope

**Status: ✅ Completed**

This document defines the MVP (Minimal Viable Product) scope for PushStream.

---

## Published Packages

| Package | Version | Registry |
|---------|---------|----------|
| [PushStream.Core](https://www.nuget.org/packages/PushStream.Core) | 0.1.0 | NuGet |
| [PushStream.AspNetCore](https://www.nuget.org/packages/PushStream.AspNetCore) | 0.1.0 | NuGet |
| [pushstream-client](https://www.npmjs.com/package/pushstream-client) | 0.1.0 | npm |

---

## MVP Features (Completed)

### Server (.NET)
- ✅ One SSE endpoint (`MapEventStream`)
- ✅ Named events with `domain.action` convention
- ✅ Push to all clients (`PublishAsync`)
- ✅ Push to specific client (`PublishToAsync`)
- ✅ In-memory connection store
- ✅ Graceful disconnect handling
- ✅ Automatic heartbeats

### Client (JavaScript)
- ✅ EventClient with auto-reconnection
- ✅ Event subscription API (`on`, `off`, `once`)
- ✅ Built-in lifecycle events (`stream.connected`, `stream.error`, etc.)
- ✅ Exponential backoff for reconnection
- ✅ Zero dependencies

### Demo Application
- ✅ Live Order Tracker demonstration
- ✅ Real-world use case example

---

## Future Scope

### Backend
- Advanced Event Broker (Redis / third-party support)
- Configurable retry intervals
- Event ID support for reconnection

### Client
- TypeScript type definitions
- React hooks integration

---

## Why No Auth Abstraction?

Authentication is intentionally excluded from PushStream's scope.
See [Security & Auth](docs/08-security-and-auth.md) for the rationale
and recommended patterns for securing your SSE endpoints.