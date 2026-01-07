/**
 * Utility functions for PushStream client.
 * Currently minimal as the library has no external dependencies.
 */

/**
 * Check if we're running in a browser environment.
 * @returns {boolean}
 */
export function isBrowser() {
  return typeof window !== 'undefined' && typeof window.document !== 'undefined';
}

/**
 * Check if EventSource is available.
 * @returns {boolean}
 */
export function isEventSourceSupported() {
  return typeof EventSource !== 'undefined';
}

/**
 * Generate a simple unique ID.
 * @returns {string}
 */
export function generateId() {
  return Date.now().toString(36) + Math.random().toString(36).substring(2);
}

/**
 * Safe JSON parse that returns null on failure.
 * @param {string} str - JSON string to parse
 * @returns {*|null} Parsed object or null
 */
export function safeJsonParse(str) {
  try {
    return JSON.parse(str);
  } catch {
    return null;
  }
}

