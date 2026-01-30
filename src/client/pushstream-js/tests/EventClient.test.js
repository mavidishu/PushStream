import { describe, it, beforeEach, afterEach, mock } from 'node:test';
import assert from 'node:assert/strict';

// Mock EventSource for Node.js environment
class MockEventSource {
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSED = 2;

  constructor(url, options = {}) {
    this.url = url;
    this.withCredentials = options.withCredentials || false;
    this.readyState = MockEventSource.CONNECTING;
    this._listeners = new Map();
    this.onopen = null;
    this.onerror = null;
    this.onmessage = null;
  }

  addEventListener(type, listener) {
    if (!this._listeners.has(type)) {
      this._listeners.set(type, []);
    }
    this._listeners.get(type).push(listener);
  }

  removeEventListener(type, listener) {
    const listeners = this._listeners.get(type);
    if (listeners) {
      const index = listeners.indexOf(listener);
      if (index > -1) {
        listeners.splice(index, 1);
      }
    }
  }

  close() {
    this.readyState = MockEventSource.CLOSED;
  }

  // Test helpers
  _simulateOpen() {
    this.readyState = MockEventSource.OPEN;
    if (this.onopen) {
      this.onopen(new Event('open'));
    }
  }

  _simulateError() {
    if (this.onerror) {
      this.onerror(new Event('error'));
    }
  }

  _simulateClose() {
    this.readyState = MockEventSource.CLOSED;
    if (this.onerror) {
      this.onerror(new Event('error'));
    }
  }

  _simulateMessage(type, data, lastEventId = null) {
    const listeners = this._listeners.get(type);
    if (listeners) {
      const event = { type, data: JSON.stringify(data), lastEventId };
      listeners.forEach(listener => listener(event));
    }
  }
}

// Set global EventSource for tests
globalThis.EventSource = MockEventSource;

// Now import after mocking
const { EventClient } = await import('../src/EventClient.js');
const { ConnectionState, BuiltInEvents } = await import('../src/constants.js');

describe('EventClient', () => {
  let client;
  let mockEventSource;

  beforeEach(() => {
    // Create client but don't connect yet
    client = new EventClient('/events');
  });

  afterEach(() => {
    if (client) {
      client.disconnect();
    }
  });

  describe('constructor', () => {
    it('should create client with URL', () => {
      const c = new EventClient('/test');
      assert.strictEqual(c.url, '/test');
      assert.strictEqual(c.state, ConnectionState.DISCONNECTED);
    });

    it('should throw TypeError for missing URL', () => {
      assert.throws(() => new EventClient(), TypeError);
      assert.throws(() => new EventClient(''), TypeError);
      assert.throws(() => new EventClient(null), TypeError);
    });

    it('should accept options', () => {
      const c = new EventClient('/test', { reconnect: false });
      assert.strictEqual(c.state, ConnectionState.DISCONNECTED);
    });

    it('should not establish connection on construction', () => {
      const c = new EventClient('/test');
      assert.strictEqual(c.state, ConnectionState.DISCONNECTED);
    });
  });

  describe('connect()', () => {
    it('should establish SSE connection', () => {
      client.connect();
      assert.strictEqual(client.state, ConnectionState.CONNECTING);
    });

    it('should be idempotent when already connecting', () => {
      client.connect();
      const state1 = client.state;
      client.connect(); // Should not create new connection
      assert.strictEqual(client.state, state1);
    });

    it('should be idempotent when already connected', () => {
      client.connect();
      client._eventSource._simulateOpen();
      assert.strictEqual(client.state, ConnectionState.CONNECTED);
      
      client.connect(); // Should not affect existing connection
      assert.strictEqual(client.state, ConnectionState.CONNECTED);
    });

    it('should handle relative URLs', () => {
      const c = new EventClient('/events');
      c.connect();
      assert.strictEqual(c._eventSource.url, '/events');
    });

    it('should handle absolute URLs', () => {
      const c = new EventClient('https://example.com/events');
      c.connect();
      assert.strictEqual(c._eventSource.url, 'https://example.com/events');
    });

    it('should handle URLs with query parameters', () => {
      const c = new EventClient('/events?token=abc&client=xyz');
      c.connect();
      assert.strictEqual(c._eventSource.url, '/events?token=abc&client=xyz');
    });

    it('should pass withCredentials option', () => {
      const c = new EventClient('/events', { withCredentials: true });
      c.connect();
      assert.strictEqual(c._eventSource.withCredentials, true);
    });
  });

  describe('disconnect()', () => {
    it('should close SSE connection', () => {
      client.connect();
      client._eventSource._simulateOpen();
      client.disconnect();
      
      assert.strictEqual(client.state, ConnectionState.DISCONNECTED);
    });

    it('should be idempotent when already disconnected', () => {
      assert.doesNotThrow(() => {
        client.disconnect();
        client.disconnect();
      });
    });

    it('should emit close event with manual flag', (t, done) => {
      client.connect();
      client._eventSource._simulateOpen();
      
      client.on(BuiltInEvents.CLOSE, (data) => {
        assert.strictEqual(data.manual, true);
        done();
      });
      
      client.disconnect();
    });

    it('should prevent automatic reconnection', async () => {
      const c = new EventClient('/events', { 
        reconnect: true, 
        reconnectInterval: 10 
      });
      
      c.connect();
      c._eventSource._simulateOpen();
      c.disconnect();
      
      // Wait a bit and verify no reconnection
      await new Promise(resolve => setTimeout(resolve, 50));
      assert.strictEqual(c.state, ConnectionState.DISCONNECTED);
    });
  });

  describe('on()', () => {
    it('should register callback for event', () => {
      const callback = () => {};
      const result = client.on('test.event', callback);
      
      assert.strictEqual(result, client); // Returns this for chaining
    });

    it('should throw TypeError for invalid event name', () => {
      assert.throws(() => client.on('', () => {}), TypeError);
      assert.throws(() => client.on(null, () => {}), TypeError);
    });

    it('should throw TypeError for invalid callback', () => {
      assert.throws(() => client.on('test', 'not a function'), TypeError);
      assert.throws(() => client.on('test', null), TypeError);
    });

    it('should invoke callback when event is received', () => {
      let received = null;
      const testData = { message: 'hello' };
      
      client.on('test.event', (data) => { received = data; });
      client.connect();
      client._eventSource._simulateOpen();
      client._eventSource._simulateMessage('test.event', testData);
      
      assert.deepStrictEqual(received, testData);
    });

    it('should support multiple subscribers for same event', () => {
      let count = 0;
      
      client.on('test.event', () => { count++; });
      client.on('test.event', () => { count++; });
      client.connect();
      client._eventSource._simulateOpen();
      client._eventSource._simulateMessage('test.event', {});
      
      assert.strictEqual(count, 2);
    });

    it('should work when subscribed before connect', () => {
      let received = false;
      
      client.on('test.event', () => { received = true; });
      client.connect();
      client._eventSource._simulateOpen();
      client._eventSource._simulateMessage('test.event', {});
      
      assert.strictEqual(received, true);
    });

    it('should work when subscribed after connect', () => {
      let received = false;
      
      client.connect();
      client._eventSource._simulateOpen();
      client.on('test.event', () => { received = true; });
      client._eventSource._simulateMessage('test.event', {});
      
      assert.strictEqual(received, true);
    });
  });

  describe('off()', () => {
    it('should remove specific callback', () => {
      let callCount = 0;
      const callback = () => { callCount++; };
      
      client.on('test.event', callback);
      client.connect();
      client._eventSource._simulateOpen();
      client._eventSource._simulateMessage('test.event', {});
      assert.strictEqual(callCount, 1);
      
      client.off('test.event', callback);
      client._eventSource._simulateMessage('test.event', {});
      assert.strictEqual(callCount, 1); // Should not increase
    });

    it('should remove all callbacks when callback not specified', () => {
      let count = 0;
      
      client.on('test.event', () => { count++; });
      client.on('test.event', () => { count++; });
      client.connect();
      client._eventSource._simulateOpen();
      
      client.off('test.event');
      client._eventSource._simulateMessage('test.event', {});
      
      assert.strictEqual(count, 0);
    });

    it('should return this for chaining', () => {
      const result = client.off('test.event');
      assert.strictEqual(result, client);
    });
  });

  describe('state property', () => {
    it('should start as disconnected', () => {
      assert.strictEqual(client.state, ConnectionState.DISCONNECTED);
    });

    it('should change to connecting when connect() called', () => {
      client.connect();
      assert.strictEqual(client.state, ConnectionState.CONNECTING);
    });

    it('should change to connected when connection opens', () => {
      client.connect();
      client._eventSource._simulateOpen();
      assert.strictEqual(client.state, ConnectionState.CONNECTED);
    });

    it('should change to disconnected when disconnect() called', () => {
      client.connect();
      client._eventSource._simulateOpen();
      client.disconnect();
      assert.strictEqual(client.state, ConnectionState.DISCONNECTED);
    });
  });

  describe('built-in events', () => {
    it('should emit stream.open when connected', () => {
      let emitted = false;
      
      client.on(BuiltInEvents.OPEN, () => { emitted = true; });
      client.connect();
      client._eventSource._simulateOpen();
      
      assert.strictEqual(emitted, true);
    });

    it('should emit stream.close when disconnected', () => {
      let emitted = false;
      
      client.connect();
      client._eventSource._simulateOpen();
      client.on(BuiltInEvents.CLOSE, () => { emitted = true; });
      client.disconnect();
      
      assert.strictEqual(emitted, true);
    });

    it('should emit stream.error on connection error', () => {
      let emitted = false;
      
      client.on(BuiltInEvents.ERROR, () => { emitted = true; });
      client.connect();
      client._eventSource._simulateError();
      
      assert.strictEqual(emitted, true);
    });

    it('should emit stream.statechange when state changes', () => {
      const stateChanges = [];
      
      client.on(BuiltInEvents.STATE_CHANGE, (data) => {
        stateChanges.push(data);
      });
      
      client.connect();
      client._eventSource._simulateOpen();
      
      assert.strictEqual(stateChanges.length, 2);
      assert.deepStrictEqual(stateChanges[0], {
        previousState: ConnectionState.DISCONNECTED,
        currentState: ConnectionState.CONNECTING
      });
      assert.deepStrictEqual(stateChanges[1], {
        previousState: ConnectionState.CONNECTING,
        currentState: ConnectionState.CONNECTED
      });
    });
  });

  describe('JSON parsing', () => {
    it('should parse JSON payloads automatically', () => {
      let received = null;
      const testData = { foo: 'bar', num: 42, nested: { a: 1 } };
      
      client.on('test.event', (data) => { received = data; });
      client.connect();
      client._eventSource._simulateOpen();
      client._eventSource._simulateMessage('test.event', testData);
      
      assert.deepStrictEqual(received, testData);
    });

    it('should emit error for invalid JSON but not disconnect', () => {
      let errorEmitted = false;
      let messageReceived = false;
      
      client.on(BuiltInEvents.ERROR, (err) => {
        if (err.message.includes('JSON')) {
          errorEmitted = true;
        }
      });
      
      client.on('test.event', () => { messageReceived = true; });
      client.connect();
      client._eventSource._simulateOpen();
      
      // Simulate invalid JSON
      const listeners = client._eventSource._listeners.get('test.event');
      if (listeners) {
        listeners.forEach(listener => listener({ type: 'test.event', data: 'not valid json{' }));
      }
      
      assert.strictEqual(errorEmitted, true);
      assert.strictEqual(messageReceived, false);
      assert.strictEqual(client.state, ConnectionState.CONNECTED); // Still connected
    });
  });

  describe('auto-reconnection', () => {
    it('should schedule reconnection on connection loss', async () => {
      const c = new EventClient('/events', { 
        reconnect: true,
        reconnectInterval: 10,
        maxReconnectAttempts: 3
      });
      
      c.connect();
      c._eventSource._simulateOpen();
      
      // Simulate connection loss
      c._eventSource._simulateClose();
      
      // Verify reconnection was scheduled (timer should exist)
      assert.notStrictEqual(c._reconnectTimer, null);
      
      c.disconnect();
    });

    it('should not reconnect after manual disconnect', async () => {
      const c = new EventClient('/events', { 
        reconnect: true,
        reconnectInterval: 10 
      });
      
      c.connect();
      c._eventSource._simulateOpen();
      c.disconnect();
      
      // Timer should be cleared after manual disconnect
      assert.strictEqual(c._reconnectTimer, null);
      assert.strictEqual(c.state, ConnectionState.DISCONNECTED);
    });

    it('should preserve subscriptions after disconnect and reconnect', () => {
      const c = new EventClient('/events', { reconnect: false });
      
      let receivedCount = 0;
      c.on('test.event', () => { receivedCount++; });
      
      c.connect();
      c._eventSource._simulateOpen();
      c._eventSource._simulateMessage('test.event', {});
      assert.strictEqual(receivedCount, 1);
      
      // Manual disconnect and reconnect
      c.disconnect();
      c.connect();
      c._eventSource._simulateOpen();
      c._eventSource._simulateMessage('test.event', {});
      
      assert.strictEqual(receivedCount, 2);
      c.disconnect();
    });

    it('should emit error when max attempts reached', () => {
      const c = new EventClient('/events', { 
        reconnect: true,
        reconnectInterval: 5,
        maxReconnectAttempts: 2
      });
      
      let maxAttemptsError = false;
      c.on(BuiltInEvents.ERROR, (err) => {
        if (err.message && err.message.includes('Max reconnection')) {
          maxAttemptsError = true;
        }
      });
      
      // Manually set attempt count to max
      c._reconnectAttempts = 2;
      c._manualDisconnect = false;
      
      // Call _scheduleReconnect - should emit error
      c._scheduleReconnect();
      
      assert.strictEqual(maxAttemptsError, true);
    });

    it('should reset attempt counter on successful connection', () => {
      const c = new EventClient('/events', { 
        reconnect: true,
        reconnectInterval: 5,
        maxReconnectAttempts: 5
      });
      
      c._reconnectAttempts = 3; // Simulate some failed attempts
      
      c.connect();
      c._eventSource._simulateOpen();
      
      // After successful connection, counter should reset to 0
      assert.strictEqual(c._reconnectAttempts, 0);
      
      c.disconnect();
    });
  });

  describe('reconnect after disconnect', () => {
    it('should allow reconnection after manual disconnect', () => {
      client.connect();
      client._eventSource._simulateOpen();
      client.disconnect();
      
      assert.strictEqual(client.state, ConnectionState.DISCONNECTED);
      
      client.connect();
      assert.strictEqual(client.state, ConnectionState.CONNECTING);
    });

    it('should work with event subscriptions after reconnect', () => {
      let count = 0;
      client.on('test.event', () => { count++; });
      
      client.connect();
      client._eventSource._simulateOpen();
      client._eventSource._simulateMessage('test.event', {});
      assert.strictEqual(count, 1);
      
      client.disconnect();
      client.connect();
      client._eventSource._simulateOpen();
      client._eventSource._simulateMessage('test.event', {});
      
      assert.strictEqual(count, 2);
    });
  });

  describe('lastEventId', () => {
    it('should start as null', () => {
      assert.strictEqual(client.lastEventId, null);
    });

    it('should remain null after connection without events', () => {
      client.connect();
      client._eventSource._simulateOpen();
      
      assert.strictEqual(client.lastEventId, null);
    });

    it('should update when event with ID is received', () => {
      client.on('test.event', () => {});
      client.connect();
      client._eventSource._simulateOpen();
      
      client._eventSource._simulateMessage('test.event', { data: 'hello' }, 'evt_123');
      
      assert.strictEqual(client.lastEventId, 'evt_123');
    });

    it('should update to latest event ID', () => {
      client.on('test.event', () => {});
      client.connect();
      client._eventSource._simulateOpen();
      
      client._eventSource._simulateMessage('test.event', { data: 'first' }, 'evt_001');
      assert.strictEqual(client.lastEventId, 'evt_001');
      
      client._eventSource._simulateMessage('test.event', { data: 'second' }, 'evt_002');
      assert.strictEqual(client.lastEventId, 'evt_002');
      
      client._eventSource._simulateMessage('test.event', { data: 'third' }, 'evt_003');
      assert.strictEqual(client.lastEventId, 'evt_003');
    });

    it('should not update when event has no ID', () => {
      client.on('test.event', () => {});
      client.connect();
      client._eventSource._simulateOpen();
      
      client._eventSource._simulateMessage('test.event', { data: 'hello' }, 'evt_123');
      assert.strictEqual(client.lastEventId, 'evt_123');
      
      // Message without event ID should not change lastEventId
      client._eventSource._simulateMessage('test.event', { data: 'world' }, null);
      assert.strictEqual(client.lastEventId, 'evt_123');
    });

    it('should persist after disconnect and reconnect', () => {
      client.on('test.event', () => {});
      client.connect();
      client._eventSource._simulateOpen();
      
      client._eventSource._simulateMessage('test.event', { data: 'hello' }, 'evt_abc');
      assert.strictEqual(client.lastEventId, 'evt_abc');
      
      client.disconnect();
      assert.strictEqual(client.lastEventId, 'evt_abc');
      
      client.connect();
      client._eventSource._simulateOpen();
      assert.strictEqual(client.lastEventId, 'evt_abc');
    });

    it('should be accessible from event callback', () => {
      let capturedId = null;
      
      client.on('test.event', () => {
        capturedId = client.lastEventId;
      });
      
      client.connect();
      client._eventSource._simulateOpen();
      client._eventSource._simulateMessage('test.event', { data: 'hello' }, 'evt_xyz');
      
      assert.strictEqual(capturedId, 'evt_xyz');
    });
  });
});