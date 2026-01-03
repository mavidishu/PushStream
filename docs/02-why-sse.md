# Why Server-Sent Events (SSE)?

This project deliberately chooses **Server-Sent Events (SSE)** as the
underlying transport mechanism for real-time updates.

This document explains **why SSE is the right fit**, where alternatives
fall short, and the design philosophy behind this choice.

---

## The Real Problem

Most real-time use cases in modern web applications are **unidirectional**:

- Task status updates
- Background job progress
- Notifications
- AI/ML processing completion events
- Long-running workflows

In these scenarios:
- The **client does not need to send messages back**
- The **server only needs to push updates**
- Reliability and simplicity matter more than full duplex communication

Yet, developers frequently reach for **SignalR or WebSockets** by default.

---

## Why Not Polling?

### ❌ Polling
Polling forces the client to repeatedly ask the server for updates.

**Problems:**
- Wasted network calls
- Increased server load
- Latency between actual update and client awareness
- Complex retry and backoff logic

Polling is acceptable for simple or low-frequency checks, but it does not
scale well for real-time feedback.

---

## Why Not WebSockets or SignalR?

### ❌ WebSockets / SignalR
These technologies provide **bi-directional communication**.

While powerful, they introduce **unnecessary complexity** when only
server-to-client communication is required.

**Challenges:**
- Connection lifecycle management
- State synchronization
- Scaling complexities
- Additional infrastructure and configuration
- Over-engineering for simple use cases

> Using WebSockets for one-way updates is often equivalent to using a
> two-way radio when only a public announcement system is needed.

---

## Why SSE Fits Naturally

### ✅ Server-Sent Events (SSE)

SSE provides:
- **One-way communication** (server → client)
- Built-in **reconnection**
- Automatic **event ordering**
- Native browser support via `EventSource`
- Simple HTTP-based transport

**Key Characteristics:**
- Uses standard HTTP
- Plays well with existing infrastructure
- Easy to reason about
- Ideal for streaming updates

SSE is purpose-built for exactly the class of problems this project targets.

---

## The Catch: SSE Is Too Low-Level

Despite its advantages, SSE has a major drawback:

> SSE is powerful, but **not ergonomic**.

Developers must manually handle:
- Connection lifecycle
- Retry logic
- Heartbeats
- Event naming conventions
- Error handling
- Scaling patterns

As a result, teams often avoid SSE entirely or implement fragile,
one-off solutions.

---

## Project Philosophy

This project exists to bridge that gap.

### Goals:
- Preserve SSE’s **simplicity**
- Abstract away **boilerplate**
- Provide **opinionated defaults**
- Make server push **easy, safe, and maintainable**

### Non-Goals:
- Replacing WebSockets or SignalR
- Supporting bi-directional messaging
- Becoming a general-purpose real-time framework

---

## When You Should Use This

This library is ideal when:
- Updates flow **only from server to client**
- Clients need **instant feedback**
- Tasks are long-running or asynchronous
- You want **real-time behavior without real-time complexity**

---

## Summary

SSE is often overlooked not because it is inadequate, but because it is
**under-supported at the abstraction level**.

This project embraces SSE’s strengths while eliminating its pain points,
making real-time server push accessible without unnecessary complexity.
