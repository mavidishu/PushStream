import { ConnectionState, BuiltInEvents, DefaultOptions } from './constants.js';
import { SubscriptionManager } from './SubscriptionManager.js';

/**
 * EventClient provides a clean abstraction over EventSource for consuming SSE events.
 * 
 * Features:
 * - Automatic reconnection with exponential backoff and jitter
 * - Event subscription management
 * - Automatic JSON payload parsing
 * - Connection state tracking
 * - Built-in lifecycle events
 * 
 * @example
 * const client = new EventClient('/events');
 * client.on('task.progress', (data) => console.log(data.percentage));
 * client.connect();
 */
export class EventClient {
  /**
   * Create a new EventClient instance.
   * @param {string} url - The SSE endpoint URL (relative or absolute)
   * @param {Object} [options] - Configuration options
   * @param {boolean} [options.reconnect=true] - Enable automatic reconnection
   * @param {number} [options.reconnectInterval=1000] - Base reconnection delay in ms
   * @param {number} [options.maxReconnectAttempts=10] - Maximum reconnection attempts
   * @param {number} [options.maxReconnectDelay=30000] - Maximum delay cap in ms
   * @param {boolean} [options.withCredentials=false] - Include credentials in CORS requests
   */
  constructor(url, options = {}) {
    if (!url || typeof url !== 'string') {
      throw new TypeError('URL must be a non-empty string');
    }

    this._url = url;
    this._options = { ...DefaultOptions, ...options };
    this._eventSource = null;
    this._subscriptions = new SubscriptionManager();
    this._state = ConnectionState.DISCONNECTED;
    this._reconnectAttempts = 0;
    this._reconnectTimer = null;
    this._manualDisconnect = false;
    this._registeredEventTypes = new Set();
  }

  /**
   * Get the current connection state.
   * @returns {string} One of: 'disconnected', 'connecting', 'connected'
   */
  get state() {
    return this._state;
  }

  /**
   * Get the endpoint URL.
   * @returns {string} The SSE endpoint URL
   */
  get url() {
    return this._url;
  }

  /**
   * Establish an SSE connection to the server.
   * This method is idempotent - calling it while already connected has no effect.
   */
  connect() {
    // Idempotent: don't reconnect if already connecting or connected
    if (this._state !== ConnectionState.DISCONNECTED) {
      return;
    }

    this._manualDisconnect = false;
    this._setState(ConnectionState.CONNECTING);

    try {
      this._eventSource = new EventSource(this._url, {
        withCredentials: this._options.withCredentials
      });

      this._eventSource.onopen = () => this._handleOpen();
      this._eventSource.onerror = (event) => this._handleError(event);

      // Register event listeners for all currently subscribed event types
      this._registerEventListeners();
    } catch (error) {
      this._handleConnectionError(error);
    }
  }

  /**
   * Close the SSE connection.
   * This method is idempotent - calling it while already disconnected has no effect.
   * After calling disconnect(), no automatic reconnection will be attempted.
   */
  disconnect() {
    this._manualDisconnect = true;
    this._cleanup();
    
    if (this._state !== ConnectionState.DISCONNECTED) {
      this._setState(ConnectionState.DISCONNECTED);
      this._emit(BuiltInEvents.CLOSE, { manual: true });
    }
  }

  /**
   * Subscribe to an event.
   * Subscriptions can be registered before or after connecting.
   * @param {string} event - The event name to subscribe to
   * @param {Function} callback - The callback to invoke when the event occurs
   * @returns {EventClient} This instance for chaining
   */
  on(event, callback) {
    if (typeof event !== 'string' || !event) {
      throw new TypeError('Event name must be a non-empty string');
    }
    if (typeof callback !== 'function') {
      throw new TypeError('Callback must be a function');
    }

    this._subscriptions.add(event, callback);

    // If already connected and this is a new event type, register it
    if (this._eventSource && !this._isBuiltInEvent(event) && !this._registeredEventTypes.has(event)) {
      this._registerSingleEventListener(event);
    }

    return this;
  }

  /**
   * Unsubscribe from an event.
   * @param {string} event - The event name
   * @param {Function} [callback] - Specific callback to remove. If omitted, removes all callbacks for the event.
   * @returns {EventClient} This instance for chaining
   */
  off(event, callback) {
    if (typeof event !== 'string' || !event) {
      throw new TypeError('Event name must be a non-empty string');
    }

    if (callback !== undefined) {
      this._subscriptions.remove(event, callback);
    } else {
      this._subscriptions.removeAll(event);
    }

    return this;
  }

  // =====================
  // Private Methods
  // =====================

  /**
   * Update connection state and emit state change event.
   * @private
   */
  _setState(newState) {
    const oldState = this._state;
    if (oldState === newState) {
      return;
    }

    this._state = newState;
    this._emit(BuiltInEvents.STATE_CHANGE, {
      previousState: oldState,
      currentState: newState
    });
  }

  /**
   * Emit an event to all subscribers.
   * @private
   */
  _emit(event, data) {
    this._subscriptions.emit(event, data);
  }

  /**
   * Handle successful connection.
   * @private
   */
  _handleOpen() {
    this._reconnectAttempts = 0; // Reset on successful connection
    this._setState(ConnectionState.CONNECTED);
    this._emit(BuiltInEvents.OPEN, { url: this._url });
  }

  /**
   * Handle connection error.
   * @private
   */
  _handleError(event) {
    // EventSource error event doesn't provide much detail
    const errorInfo = {
      message: 'Connection error',
      readyState: this._eventSource?.readyState
    };

    this._emit(BuiltInEvents.ERROR, errorInfo);

    // Check if connection was lost
    if (this._eventSource?.readyState === EventSource.CLOSED) {
      this._handleConnectionLoss();
    }
  }

  /**
   * Handle initial connection failure.
   * @private
   */
  _handleConnectionError(error) {
    this._setState(ConnectionState.DISCONNECTED);
    this._emit(BuiltInEvents.ERROR, {
      message: error.message || 'Failed to create connection',
      error
    });
  }

  /**
   * Handle connection loss and schedule reconnection.
   * @private
   */
  _handleConnectionLoss() {
    this._cleanup();
    this._setState(ConnectionState.DISCONNECTED);
    this._emit(BuiltInEvents.CLOSE, { manual: false });

    // Schedule reconnection if enabled and not manually disconnected
    if (this._options.reconnect && !this._manualDisconnect) {
      this._scheduleReconnect();
    }
  }

  /**
   * Schedule a reconnection attempt with exponential backoff and jitter.
   * @private
   */
  _scheduleReconnect() {
    if (this._manualDisconnect) {
      return;
    }

    if (this._reconnectAttempts >= this._options.maxReconnectAttempts) {
      this._emit(BuiltInEvents.ERROR, {
        message: 'Max reconnection attempts reached',
        attempts: this._reconnectAttempts
      });
      return;
    }

    // Exponential backoff: interval * 2^attempts
    const exponentialDelay = this._options.reconnectInterval * Math.pow(2, this._reconnectAttempts);
    
    // Cap at max delay
    const cappedDelay = Math.min(exponentialDelay, this._options.maxReconnectDelay);
    
    // Add jitter (0-1000ms random) to prevent thundering herd
    const jitter = Math.random() * 1000;
    const finalDelay = cappedDelay + jitter;

    this._reconnectTimer = setTimeout(() => {
      this._reconnectAttempts++;
      this.connect();
    }, finalDelay);
  }

  /**
   * Register EventSource listeners for all subscribed event types.
   * @private
   */
  _registerEventListeners() {
    const events = this._subscriptions.getEvents();
    
    for (const event of events) {
      if (!this._isBuiltInEvent(event)) {
        this._registerSingleEventListener(event);
      }
    }
  }

  /**
   * Register a single event listener on the EventSource.
   * @private
   */
  _registerSingleEventListener(event) {
    if (!this._eventSource || this._registeredEventTypes.has(event)) {
      return;
    }

    this._registeredEventTypes.add(event);
    
    this._eventSource.addEventListener(event, (sseEvent) => {
      this._handleMessage(event, sseEvent);
    });
  }

  /**
   * Handle incoming SSE message.
   * @private
   */
  _handleMessage(eventType, sseEvent) {
    let data;
    
    try {
      // Attempt JSON parsing
      data = JSON.parse(sseEvent.data);
    } catch (parseError) {
      // JSON parsing failed - emit error but don't disconnect
      this._emit(BuiltInEvents.ERROR, {
        message: 'Failed to parse JSON payload',
        eventType,
        originalData: sseEvent.data,
        error: parseError.message
      });
      return;
    }

    // Emit the parsed event to subscribers
    this._emit(eventType, data);
  }

  /**
   * Check if an event name is a built-in event.
   * @private
   */
  _isBuiltInEvent(event) {
    return event.startsWith('stream.');
  }

  /**
   * Clean up resources.
   * @private
   */
  _cleanup() {
    // Clear reconnection timer
    if (this._reconnectTimer) {
      clearTimeout(this._reconnectTimer);
      this._reconnectTimer = null;
    }

    // Close EventSource
    if (this._eventSource) {
      this._eventSource.close();
      this._eventSource = null;
    }

    // Clear registered event types (will be re-registered on reconnect)
    this._registeredEventTypes.clear();
  }
}

