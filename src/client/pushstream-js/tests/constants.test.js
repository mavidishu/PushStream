import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { ConnectionState, BuiltInEvents, DefaultOptions } from '../src/constants.js';

describe('constants', () => {
  describe('ConnectionState', () => {
    it('should have DISCONNECTED state', () => {
      assert.strictEqual(ConnectionState.DISCONNECTED, 'disconnected');
    });

    it('should have CONNECTING state', () => {
      assert.strictEqual(ConnectionState.CONNECTING, 'connecting');
    });

    it('should have CONNECTED state', () => {
      assert.strictEqual(ConnectionState.CONNECTED, 'connected');
    });

    it('should be frozen', () => {
      assert.strictEqual(Object.isFrozen(ConnectionState), true);
    });

    it('should not allow modifications', () => {
      const original = ConnectionState.DISCONNECTED;
      
      // In strict mode, attempting to modify a frozen object throws
      assert.throws(() => {
        'use strict';
        ConnectionState.DISCONNECTED = 'modified';
      }, TypeError);
      
      assert.strictEqual(ConnectionState.DISCONNECTED, original);
    });
  });

  describe('BuiltInEvents', () => {
    it('should have OPEN event', () => {
      assert.strictEqual(BuiltInEvents.OPEN, 'stream.open');
    });

    it('should have CLOSE event', () => {
      assert.strictEqual(BuiltInEvents.CLOSE, 'stream.close');
    });

    it('should have ERROR event', () => {
      assert.strictEqual(BuiltInEvents.ERROR, 'stream.error');
    });

    it('should have STATE_CHANGE event', () => {
      assert.strictEqual(BuiltInEvents.STATE_CHANGE, 'stream.statechange');
    });

    it('should be frozen', () => {
      assert.strictEqual(Object.isFrozen(BuiltInEvents), true);
    });

    it('should use stream. prefix for all events', () => {
      const allStartWithStream = Object.values(BuiltInEvents).every(
        event => event.startsWith('stream.')
      );
      assert.strictEqual(allStartWithStream, true);
    });
  });

  describe('DefaultOptions', () => {
    it('should have reconnect enabled by default', () => {
      assert.strictEqual(DefaultOptions.reconnect, true);
    });

    it('should have reconnectInterval of 1000ms', () => {
      assert.strictEqual(DefaultOptions.reconnectInterval, 1000);
    });

    it('should have maxReconnectAttempts of 10', () => {
      assert.strictEqual(DefaultOptions.maxReconnectAttempts, 10);
    });

    it('should have maxReconnectDelay of 30000ms', () => {
      assert.strictEqual(DefaultOptions.maxReconnectDelay, 30000);
    });

    it('should have withCredentials false by default', () => {
      assert.strictEqual(DefaultOptions.withCredentials, false);
    });

    it('should be frozen', () => {
      assert.strictEqual(Object.isFrozen(DefaultOptions), true);
    });
  });
});

