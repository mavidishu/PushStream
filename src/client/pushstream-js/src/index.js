/**
 * PushStream JavaScript Client
 * 
 * A lightweight, zero-dependency SSE client library.
 * 
 * @example
 * import { EventClient } from 'pushstream-js';
 * 
 * const client = new EventClient('/events');
 * client.on('task.progress', (data) => console.log(data.percentage));
 * client.on('stream.error', (error) => console.error(error.message));
 * client.connect();
 * 
 * @module pushstream-js
 */

export { EventClient } from './EventClient.js';
export { SubscriptionManager } from './SubscriptionManager.js';
export { ConnectionState, BuiltInEvents, DefaultOptions } from './constants.js';

