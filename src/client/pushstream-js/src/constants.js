/**
 * Connection states for EventClient
 * @readonly
 * @enum {string}
 */
export const ConnectionState = Object.freeze({
  /** Client is not connected */
  DISCONNECTED: 'disconnected',
  /** Client is attempting to connect */
  CONNECTING: 'connecting',
  /** Client is connected and receiving events */
  CONNECTED: 'connected'
});

/**
 * Built-in event names emitted by EventClient
 * @readonly
 * @enum {string}
 */
export const BuiltInEvents = Object.freeze({
  /** Emitted when connection is established */
  OPEN: 'stream.open',
  /** Emitted when connection is closed */
  CLOSE: 'stream.close',
  /** Emitted when an error occurs */
  ERROR: 'stream.error',
  /** Emitted when connection state changes */
  STATE_CHANGE: 'stream.statechange'
});

/**
 * Default options for EventClient
 * @readonly
 */
export const DefaultOptions = Object.freeze({
  /** Whether to automatically reconnect on connection loss */
  reconnect: true,
  /** Base delay in milliseconds between reconnection attempts */
  reconnectInterval: 1000,
  /** Maximum number of reconnection attempts before giving up */
  maxReconnectAttempts: 10,
  /** Maximum delay cap for exponential backoff (30 seconds) */
  maxReconnectDelay: 30000,
  /** Whether to include credentials in cross-origin requests */
  withCredentials: false
});

