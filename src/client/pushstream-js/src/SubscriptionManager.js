/**
 * Manages event subscriptions with O(1) add/remove operations.
 * Uses Map for event->callbacks storage and Set for callback deduplication.
 */
export class SubscriptionManager {
  constructor() {
    /** @type {Map<string, Set<Function>>} */
    this._listeners = new Map();
  }

  /**
   * Register a callback for a specific event.
   * @param {string} event - The event name to subscribe to
   * @param {Function} callback - The callback to invoke when the event occurs
   * @returns {boolean} True if the callback was added, false if already exists
   */
  add(event, callback) {
    if (typeof callback !== 'function') {
      throw new TypeError('Callback must be a function');
    }

    if (!this._listeners.has(event)) {
      this._listeners.set(event, new Set());
    }

    const callbacks = this._listeners.get(event);
    const existed = callbacks.has(callback);
    callbacks.add(callback);
    
    return !existed;
  }

  /**
   * Remove a specific callback for an event.
   * @param {string} event - The event name
   * @param {Function} callback - The callback to remove
   * @returns {boolean} True if the callback was removed
   */
  remove(event, callback) {
    const callbacks = this._listeners.get(event);
    if (!callbacks) {
      return false;
    }

    const removed = callbacks.delete(callback);
    
    // Clean up empty sets
    if (callbacks.size === 0) {
      this._listeners.delete(event);
    }

    return removed;
  }

  /**
   * Remove all callbacks for a specific event.
   * @param {string} event - The event name
   * @returns {boolean} True if any callbacks were removed
   */
  removeAll(event) {
    return this._listeners.delete(event);
  }

  /**
   * Emit an event to all registered callbacks.
   * Creates a snapshot of callbacks to allow safe modification during iteration.
   * @param {string} event - The event name
   * @param {*} data - The data to pass to callbacks
   */
  emit(event, data) {
    const callbacks = this._listeners.get(event);
    if (!callbacks || callbacks.size === 0) {
      return;
    }

    // Create snapshot to allow modifications during iteration
    const snapshot = Array.from(callbacks);
    
    for (const callback of snapshot) {
      try {
        callback(data);
      } catch (error) {
        // Log but don't throw to prevent one bad callback from breaking others
        console.error(`Error in event callback for "${event}":`, error);
      }
    }
  }

  /**
   * Check if an event has any subscribers.
   * @param {string} event - The event name
   * @returns {boolean} True if the event has subscribers
   */
  has(event) {
    const callbacks = this._listeners.get(event);
    return callbacks !== undefined && callbacks.size > 0;
  }

  /**
   * Get all registered event names.
   * @returns {string[]} Array of event names
   */
  getEvents() {
    return Array.from(this._listeners.keys());
  }

  /**
   * Get the number of callbacks for a specific event.
   * @param {string} event - The event name
   * @returns {number} Number of callbacks
   */
  getCount(event) {
    const callbacks = this._listeners.get(event);
    return callbacks ? callbacks.size : 0;
  }

  /**
   * Clear all subscriptions.
   */
  clear() {
    this._listeners.clear();
  }
}

