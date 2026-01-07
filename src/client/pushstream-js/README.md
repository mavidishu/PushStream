# pushstream-js

A lightweight, zero-dependency JavaScript client for consuming Server-Sent Events (SSE) with automatic reconnection, event subscriptions, and connection state management.

## Features

- **Zero dependencies** - Pure JavaScript, no external libraries
- **Tiny footprint** - Less than 5KB minified
- **Auto-reconnection** - Exponential backoff with jitter to prevent thundering herd
- **Event-driven API** - Familiar `on()`/`off()` subscription pattern
- **JSON parsing** - Automatic payload parsing
- **Connection state** - Track connection status with built-in events
- **Universal** - Works in browsers and Node.js

## Installation

### npm / yarn / pnpm

```bash
npm install pushstream-js
# or
yarn add pushstream-js
# or
pnpm add pushstream-js
```

### CDN / Script Tag

```html
<script src="https://unpkg.com/pushstream-js/dist/pushstream.min.js"></script>
<script>
  const client = new PushStream.EventClient('/events');
</script>
```

## Quick Start

```javascript
import { EventClient } from 'pushstream-js';

// Create client
const client = new EventClient('/events');

// Subscribe to events
client.on('task.progress', (data) => {
  console.log(`Task ${data.taskId}: ${data.percentage}%`);
});

client.on('task.complete', (data) => {
  console.log(`Task ${data.taskId} completed!`);
});

// Handle errors
client.on('stream.error', (error) => {
  console.error('Connection error:', error.message);
});

// Connect
client.connect();

// Later: disconnect
client.disconnect();
```

## API Reference

### `new EventClient(url, options?)`

Creates a new EventClient instance.

**Parameters:**
- `url` (string) - The SSE endpoint URL (relative or absolute)
- `options` (object, optional):
  - `reconnect` (boolean, default: `true`) - Enable automatic reconnection
  - `reconnectInterval` (number, default: `1000`) - Base delay in milliseconds
  - `maxReconnectAttempts` (number, default: `10`) - Maximum retry attempts
  - `maxReconnectDelay` (number, default: `30000`) - Maximum backoff delay
  - `withCredentials` (boolean, default: `false`) - Include cookies in CORS requests

```javascript
const client = new EventClient('/events', {
  reconnect: true,
  reconnectInterval: 2000,
  maxReconnectAttempts: 5
});
```

### `connect()`

Establish an SSE connection to the server. This method is idempotent - calling it while already connected has no effect.

```javascript
client.connect();
```

### `disconnect()`

Close the SSE connection. After calling `disconnect()`, no automatic reconnection will be attempted.

```javascript
client.disconnect();
```

### `on(event, callback)`

Subscribe to an event. Returns the client instance for chaining.

```javascript
client
  .on('task.progress', handleProgress)
  .on('task.complete', handleComplete);
```

### `off(event, callback?)`

Unsubscribe from an event. If `callback` is omitted, removes all listeners for that event.

```javascript
// Remove specific callback
client.off('task.progress', handleProgress);

// Remove all callbacks for event
client.off('task.progress');
```

### `state` (property)

Get the current connection state.

```javascript
console.log(client.state); // 'disconnected' | 'connecting' | 'connected'
```

## Built-in Events

| Event | Description | Payload |
|-------|-------------|---------|
| `stream.open` | Connection established | `{ url: string }` |
| `stream.close` | Connection closed | `{ manual: boolean }` |
| `stream.error` | Error occurred | `{ message: string, ... }` |
| `stream.statechange` | State changed | `{ previousState, currentState }` |

```javascript
client.on('stream.open', () => {
  console.log('Connected!');
});

client.on('stream.close', ({ manual }) => {
  console.log(manual ? 'Disconnected by user' : 'Connection lost');
});

client.on('stream.error', ({ message }) => {
  console.error('Error:', message);
});

client.on('stream.statechange', ({ previousState, currentState }) => {
  console.log(`State: ${previousState} -> ${currentState}`);
});
```

## Connection States

| State | Description |
|-------|-------------|
| `disconnected` | Not connected to server |
| `connecting` | Attempting to establish connection |
| `connected` | Connected and receiving events |

## Reconnection Behavior

By default, the client automatically reconnects when the connection is lost:

1. Uses **exponential backoff**: delays increase with each failed attempt
2. Adds **jitter** (random delay) to prevent all clients reconnecting simultaneously
3. Respects **max attempts**: stops after `maxReconnectAttempts` failures
4. **Preserves subscriptions**: no need to re-register event handlers after reconnection

To disable auto-reconnection:

```javascript
const client = new EventClient('/events', { reconnect: false });
```

## Authentication

Since `EventSource` cannot send custom headers, use query parameters for authentication:

```javascript
const client = new EventClient('/events?token=your-jwt-token');
client.connect();
```

For cookie-based auth with CORS:

```javascript
const client = new EventClient('https://api.example.com/events', {
  withCredentials: true
});
```

## Browser Support

| Browser | Version |
|---------|---------|
| Chrome | 60+ |
| Firefox | 55+ |
| Safari | 11+ |
| Edge | 79+ |

## Node.js Support

For Node.js 18+, use with an EventSource polyfill:

```javascript
import { EventSource } from 'eventsource';
globalThis.EventSource = EventSource;

import { EventClient } from 'pushstream-js';
// ... use as normal
```

## Examples

### Progress Tracking

```javascript
const client = new EventClient('/api/upload/events?uploadId=abc123');

client.on('upload.progress', ({ percentage, bytesUploaded }) => {
  updateProgressBar(percentage);
});

client.on('upload.complete', ({ fileUrl }) => {
  showSuccess(fileUrl);
  client.disconnect();
});

client.on('upload.error', ({ message }) => {
  showError(message);
  client.disconnect();
});

client.connect();
```

### Live Notifications

```javascript
const client = new EventClient('/api/notifications');

client.on('notification', ({ title, body, type }) => {
  showNotification(title, body, type);
});

client.on('stream.error', () => {
  showOfflineIndicator();
});

client.on('stream.open', () => {
  hideOfflineIndicator();
});

client.connect();
```

## TypeScript

TypeScript definitions are planned for a future release. For now, you can create a basic `.d.ts` file:

```typescript
declare module 'pushstream-js' {
  export class EventClient {
    constructor(url: string, options?: EventClientOptions);
    connect(): void;
    disconnect(): void;
    on(event: string, callback: (data: any) => void): this;
    off(event: string, callback?: (data: any) => void): this;
    readonly state: 'disconnected' | 'connecting' | 'connected';
  }

  export interface EventClientOptions {
    reconnect?: boolean;
    reconnectInterval?: number;
    maxReconnectAttempts?: number;
    maxReconnectDelay?: number;
    withCredentials?: boolean;
  }
}
```

## License

MIT

