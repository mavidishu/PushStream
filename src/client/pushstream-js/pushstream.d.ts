// Type definitions for pushstream-client
// Project: https://github.com/mavidishu/PushStream
// Definitions by: Dishu Mavi

// =============================================================================
// Options
// =============================================================================

/**
 * Configuration options for EventClient.
 */
export interface EventClientOptions {
  /**
   * Whether to automatically reconnect on connection loss.
   * @default true
   */
  reconnect?: boolean;

  /**
   * Base delay in milliseconds between reconnection attempts.
   * @default 1000
   */
  reconnectInterval?: number;

  /**
   * Maximum number of reconnection attempts before giving up.
   * @default 10
   */
  maxReconnectAttempts?: number;

  /**
   * Maximum delay cap for exponential backoff in milliseconds.
   * @default 30000
   */
  maxReconnectDelay?: number;

  /**
   * Whether to include credentials in cross-origin requests.
   * @default false
   */
  withCredentials?: boolean;
}

// =============================================================================
// Types
// =============================================================================

/**
 * Callback function type for event handlers.
 * @template T - The type of the event data payload
 */
export type EventCallback<T = unknown> = (data: T) => void;

/**
 * Possible values for connection state.
 */
export type ConnectionStateValue = 'disconnected' | 'connecting' | 'connected';

/**
 * Built-in event name values.
 */
export type BuiltInEventValue = 
  | 'stream.open'
  | 'stream.close'
  | 'stream.error'
  | 'stream.statechange';

// =============================================================================
// Constants
// =============================================================================

/**
 * Connection states for EventClient.
 */
export const ConnectionState: Readonly<{
  /** Client is not connected */
  DISCONNECTED: 'disconnected';
  /** Client is attempting to connect */
  CONNECTING: 'connecting';
  /** Client is connected and receiving events */
  CONNECTED: 'connected';
}>;

/**
 * Built-in event names emitted by EventClient.
 */
export const BuiltInEvents: Readonly<{
  /** Emitted when connection is established */
  OPEN: 'stream.open';
  /** Emitted when connection is closed */
  CLOSE: 'stream.close';
  /** Emitted when an error occurs */
  ERROR: 'stream.error';
  /** Emitted when connection state changes */
  STATE_CHANGE: 'stream.statechange';
}>;

/**
 * Default options for EventClient.
 */
export const DefaultOptions: Readonly<{
  /** Whether to automatically reconnect on connection loss */
  reconnect: true;
  /** Base delay in milliseconds between reconnection attempts */
  reconnectInterval: 1000;
  /** Maximum number of reconnection attempts before giving up */
  maxReconnectAttempts: 10;
  /** Maximum delay cap for exponential backoff (30 seconds) */
  maxReconnectDelay: 30000;
  /** Whether to include credentials in cross-origin requests */
  withCredentials: false;
}>;

// =============================================================================
// Classes
// =============================================================================

/**
 * EventClient provides a clean abstraction over EventSource for consuming SSE events.
 *
 * Features:
 * - Automatic reconnection with exponential backoff and jitter
 * - Event subscription management
 * - Automatic JSON payload parsing
 * - Connection state tracking
 * - Built-in lifecycle events
 * - Server-controlled retry intervals (via SSE `retry:` field)
 *
 * Note: The server sends a `retry:` field at connection time which the browser's
 * native EventSource uses automatically for reconnection timing. This is handled
 * transparently by the browser - the client options provide additional control
 * for exponential backoff on top of the server-specified base interval.
 *
 * @example
 * ```typescript
 * import { EventClient } from 'pushstream-client';
 *
 * interface TaskProgress {
 *   taskId: string;
 *   percentage: number;
 * }
 *
 * const client = new EventClient('/events');
 *
 * client.on<TaskProgress>('task.progress', (data) => {
 *   console.log(`Task ${data.taskId}: ${data.percentage}%`);
 * });
 *
 * client.connect();
 * ```
 */
export class EventClient {
  /**
   * Create a new EventClient instance.
   * @param url - The SSE endpoint URL (relative or absolute)
   * @param options - Configuration options
   */
  constructor(url: string, options?: EventClientOptions);

  /**
   * Get the current connection state.
   */
  readonly state: ConnectionStateValue;

  /**
   * Get the endpoint URL.
   */
  readonly url: string;

  /**
   * Get the last event ID received from the server.
   * Used for reconnection support - the browser will automatically send this
   * as the Last-Event-ID header when reconnecting.
   */
  readonly lastEventId: string | null;

  /**
   * Establish an SSE connection to the server.
   * This method is idempotent - calling it while already connected has no effect.
   */
  connect(): void;

  /**
   * Close the SSE connection.
   * This method is idempotent - calling it while already disconnected has no effect.
   * After calling disconnect(), no automatic reconnection will be attempted.
   */
  disconnect(): void;

  /**
   * Subscribe to an event.
   * Subscriptions can be registered before or after connecting.
   *
   * @template T - The type of the event data payload
   * @param event - The event name to subscribe to (e.g., 'order.updated')
   * @param callback - The callback to invoke when the event occurs
   * @returns This instance for method chaining
   *
   * @example
   * ```typescript
   * interface OrderUpdate {
   *   orderId: string;
   *   status: string;
   * }
   *
   * client.on<OrderUpdate>('order.updated', (data) => {
   *   console.log(data.orderId); // Fully typed!
   * });
   * ```
   */
  on<T = unknown>(event: string, callback: EventCallback<T>): this;

  /**
   * Unsubscribe from an event.
   * @param event - The event name
   * @param callback - Specific callback to remove. If omitted, removes all callbacks for the event.
   * @returns This instance for method chaining
   */
  off<T = unknown>(event: string, callback?: EventCallback<T>): this;
}

/**
 * Manages event subscriptions with O(1) add/remove operations.
 * Uses Map for event->callbacks storage and Set for callback deduplication.
 */
export class SubscriptionManager {
  /**
   * Create a new SubscriptionManager instance.
   */
  constructor();

  /**
   * Register a callback for a specific event.
   * @param event - The event name to subscribe to
   * @param callback - The callback to invoke when the event occurs
   * @returns True if the callback was added, false if already exists
   */
  add<T = unknown>(event: string, callback: EventCallback<T>): boolean;

  /**
   * Remove a specific callback for an event.
   * @param event - The event name
   * @param callback - The callback to remove
   * @returns True if the callback was removed
   */
  remove<T = unknown>(event: string, callback: EventCallback<T>): boolean;

  /**
   * Remove all callbacks for a specific event.
   * @param event - The event name
   * @returns True if any callbacks were removed
   */
  removeAll(event: string): boolean;

  /**
   * Emit an event to all registered callbacks.
   * Creates a snapshot of callbacks to allow safe modification during iteration.
   * @param event - The event name
   * @param data - The data to pass to callbacks
   */
  emit<T = unknown>(event: string, data: T): void;

  /**
   * Check if an event has any subscribers.
   * @param event - The event name
   * @returns True if the event has subscribers
   */
  has(event: string): boolean;

  /**
   * Get all registered event names.
   * @returns Array of event names
   */
  getEvents(): string[];

  /**
   * Get the number of callbacks for a specific event.
   * @param event - The event name
   * @returns Number of callbacks
   */
  getCount(event: string): number;

  /**
   * Clear all subscriptions.
   */
  clear(): void;
}

// =============================================================================
// Built-in Event Data Types
// =============================================================================

/**
 * Data payload for 'stream.open' event.
 */
export interface StreamOpenEvent {
  /** The SSE endpoint URL */
  url: string;
}

/**
 * Data payload for 'stream.close' event.
 */
export interface StreamCloseEvent {
  /** Whether the disconnection was manual (via disconnect()) */
  manual: boolean;
}

/**
 * Data payload for 'stream.error' event.
 */
export interface StreamErrorEvent {
  /** Error message */
  message: string;
  /** Additional error details */
  [key: string]: unknown;
}

/**
 * Data payload for 'stream.statechange' event.
 */
export interface StreamStateChangeEvent {
  /** Previous connection state */
  previousState: ConnectionStateValue;
  /** Current connection state */
  currentState: ConnectionStateValue;
}