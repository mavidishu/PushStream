## Problem Statement

Developers often rely on SignalR or polling mechanisms for server-to-client
real-time updates, even when bi-directional communication is unnecessary.

While Server-Sent Events (SSE) provide a simpler alternative, they are too
low-level and require repetitive boilerplate to use safely and effectively.

This project aims to provide a clean, opinionated abstraction over SSE
that enables developers to implement real-time updates with minimal effort.