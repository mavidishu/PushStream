# Future Roadmap

This document outlines the planned evolution of PushStream, organized into
phases that build progressively toward a production-ready, enterprise-capable
real-time event streaming solution.

---

## Guiding Principles

All roadmap decisions are guided by:

1. **Simplicity first** — Don't add complexity unless there's clear demand
2. **Backward compatibility** — Protect existing integrations
3. **Solve real problems** — Features must address actual developer pain points
4. **Stay focused** — Unidirectional only, no scope creep into bidirectional

---

## Phase 1: MVP (Current Focus)

**Goal:** Prove the concept works and provides value.

| Feature | Status | Description |
|---------|--------|-------------|
| Single SSE endpoint | 🔲 Planned | One `/events` endpoint per application |
| Named events | 🔲 Planned | `domain.action` event naming convention |
| In-memory connection store | 🔲 Planned | Track active client connections |
| User/session targeting | 🔲 Planned | Publish to specific clients |
| Graceful disconnect | 🔲 Planned | Clean up on client disconnect |
| Auto heartbeat | 🔲 Planned | Keep connections alive |
| JavaScript client | 🔲 Planned | Thin wrapper over EventSource |

**Success Criteria:**
- End-to-end demo working
- < 50 lines of code to integrate
- Zero configuration required for basic use

---

## Phase 2: Production Hardening

**Goal:** Make it reliable enough for production workloads.

| Feature | Priority | Description |
|---------|----------|-------------|
| Connection health monitoring | High | Detect and clean up stale connections |
| Configurable retry intervals | High | `retry:` field in SSE protocol |
| Event ID support | High | `id:` field for client reconnection |
| Structured logging | High | Integration with ILogger |
| Metrics/telemetry | Medium | Connection counts, event throughput |
| Backpressure handling | Medium | What happens when client is slow |
| Unit test coverage | High | Core logic fully tested |
| Integration tests | Medium | End-to-end scenario tests |

**Success Criteria:**
- Can run in production for 7+ days without memory leaks
- Reconnection works seamlessly
- Observable via standard logging/metrics

---

## Phase 3: Scaling Support

**Goal:** Enable horizontal scaling beyond a single server.

| Feature | Priority | Description |
|---------|----------|-------------|
| Redis backplane | High | Pub/sub across multiple server instances |
| Azure SignalR backplane | Medium | Alternative for Azure-hosted apps |
| Sticky sessions guidance | Medium | Documentation for load balancer config |
| Connection affinity | Low | Optional client-to-server pinning |

**Success Criteria:**
- Works behind a load balancer with 3+ instances
- No message loss during server restarts
- Clear scaling documentation

---

## Phase 4: Developer Experience

**Goal:** Make it delightful to use.

| Feature | Priority | Description |
|---------|----------|-------------|
| TypeScript client | High | Full type safety for JS/TS developers |
| React hooks | Medium | `usePushStream()` for React apps |
| Vue composables | Low | `usePushStream()` for Vue apps |
| Event catalog/schema | Medium | Define events with types |
| Dev dashboard | Low | Local UI to inspect events in development |
| Source generators | Medium | Generate typed event classes from schema |

**Success Criteria:**
- First-class TypeScript support
- Framework integrations feel native
- Developer feedback is positive

---

## Phase 5: Enterprise Features

**Goal:** Support enterprise adoption requirements.

| Feature | Priority | Description |
|---------|----------|-------------|
| Event persistence | Medium | Store events for replay |
| Guaranteed delivery | Medium | At-least-once semantics |
| Event filtering | Medium | Server-side event subscription filters |
| Multi-tenancy | Low | Isolated event streams per tenant |
| Audit logging | Low | Track who received what events |
| Compliance helpers | Low | GDPR event cleanup, etc. |

**Success Criteria:**
- Suitable for regulated industries
- Enterprise sales objections addressed

---

## Explicitly NOT on Roadmap

These features are **intentionally excluded** to maintain focus:

| Feature | Reason |
|---------|--------|
| Bidirectional messaging | Use SignalR/WebSockets instead |
| Request-response patterns | Not SSE's purpose |
| Client-to-client messaging | Out of scope |
| Message queuing | Use RabbitMQ, Azure Service Bus, etc. |
| Offline sync | Complex state management, different problem |
| Mobile SDKs | Focus on web first |

---

## Release Timeline (Tentative)

| Phase | Target | Milestone |
|-------|--------|-----------|
| Phase 1 (MVP) | Q1 2026 | First public release |
| Phase 2 (Production) | Q2 2026 | v1.0 stable |
| Phase 3 (Scaling) | Q3 2026 | v1.x with Redis |
| Phase 4 (DX) | Q4 2026 | TypeScript + React |
| Phase 5 (Enterprise) | 2027 | v2.0 |

---

## How to Influence the Roadmap

The roadmap is driven by community feedback:

1. **GitHub Issues** — Request features, report problems
2. **Discussions** — Share use cases and requirements
3. **Pull Requests** — Contribute directly

Features with demonstrated demand get prioritized.

---

## Summary

PushStream's roadmap is intentionally conservative. Each phase builds on
proven foundations rather than rushing to add features.

**The goal is not to be the most feature-rich — it's to be the most
reliable and easiest to use solution for server-to-client updates.**

