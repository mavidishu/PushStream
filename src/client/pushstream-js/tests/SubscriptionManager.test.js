import { describe, it, beforeEach, mock } from 'node:test';
import assert from 'node:assert/strict';
import { SubscriptionManager } from '../src/SubscriptionManager.js';

describe('SubscriptionManager', () => {
  let manager;

  beforeEach(() => {
    manager = new SubscriptionManager();
  });

  describe('add()', () => {
    it('should add a callback for an event', () => {
      const callback = () => {};
      const added = manager.add('test.event', callback);
      
      assert.strictEqual(added, true);
      assert.strictEqual(manager.has('test.event'), true);
    });

    it('should return false when adding duplicate callback', () => {
      const callback = () => {};
      manager.add('test.event', callback);
      const addedAgain = manager.add('test.event', callback);
      
      assert.strictEqual(addedAgain, false);
      assert.strictEqual(manager.getCount('test.event'), 1);
    });

    it('should allow multiple different callbacks for same event', () => {
      manager.add('test.event', () => {});
      manager.add('test.event', () => {});
      
      assert.strictEqual(manager.getCount('test.event'), 2);
    });

    it('should throw TypeError for non-function callback', () => {
      assert.throws(() => {
        manager.add('test.event', 'not a function');
      }, TypeError);
    });

    it('should throw TypeError for null callback', () => {
      assert.throws(() => {
        manager.add('test.event', null);
      }, TypeError);
    });
  });

  describe('remove()', () => {
    it('should remove a specific callback', () => {
      const callback = () => {};
      manager.add('test.event', callback);
      const removed = manager.remove('test.event', callback);
      
      assert.strictEqual(removed, true);
      assert.strictEqual(manager.has('test.event'), false);
    });

    it('should return false when removing non-existent callback', () => {
      const removed = manager.remove('test.event', () => {});
      assert.strictEqual(removed, false);
    });

    it('should not remove other callbacks for same event', () => {
      const callback1 = () => {};
      const callback2 = () => {};
      
      manager.add('test.event', callback1);
      manager.add('test.event', callback2);
      manager.remove('test.event', callback1);
      
      assert.strictEqual(manager.getCount('test.event'), 1);
      assert.strictEqual(manager.has('test.event'), true);
    });

    it('should clean up empty event sets', () => {
      const callback = () => {};
      manager.add('test.event', callback);
      manager.remove('test.event', callback);
      
      // Internal cleanup - getEvents should not include removed event
      assert.strictEqual(manager.getEvents().includes('test.event'), false);
    });
  });

  describe('removeAll()', () => {
    it('should remove all callbacks for an event', () => {
      manager.add('test.event', () => {});
      manager.add('test.event', () => {});
      const removed = manager.removeAll('test.event');
      
      assert.strictEqual(removed, true);
      assert.strictEqual(manager.has('test.event'), false);
    });

    it('should return false for non-existent event', () => {
      const removed = manager.removeAll('non.existent');
      assert.strictEqual(removed, false);
    });

    it('should not affect other events', () => {
      manager.add('event1', () => {});
      manager.add('event2', () => {});
      manager.removeAll('event1');
      
      assert.strictEqual(manager.has('event1'), false);
      assert.strictEqual(manager.has('event2'), true);
    });
  });

  describe('emit()', () => {
    it('should call all callbacks for an event', () => {
      let call1 = false;
      let call2 = false;
      
      manager.add('test.event', () => { call1 = true; });
      manager.add('test.event', () => { call2 = true; });
      manager.emit('test.event', {});
      
      assert.strictEqual(call1, true);
      assert.strictEqual(call2, true);
    });

    it('should pass data to callbacks', () => {
      let receivedData = null;
      const testData = { foo: 'bar', num: 42 };
      
      manager.add('test.event', (data) => { receivedData = data; });
      manager.emit('test.event', testData);
      
      assert.deepStrictEqual(receivedData, testData);
    });

    it('should not throw for non-existent event', () => {
      assert.doesNotThrow(() => {
        manager.emit('non.existent', {});
      });
    });

    it('should continue calling other callbacks if one throws', () => {
      let called = false;
      
      manager.add('test.event', () => { throw new Error('Oops'); });
      manager.add('test.event', () => { called = true; });
      
      // Should not throw
      assert.doesNotThrow(() => {
        manager.emit('test.event', {});
      });
      
      assert.strictEqual(called, true);
    });

    it('should use snapshot to allow safe modification during emit', () => {
      const results = [];
      
      manager.add('test.event', () => {
        results.push('first');
        // Try to remove during iteration
        manager.removeAll('test.event');
      });
      manager.add('test.event', () => {
        results.push('second');
      });
      
      manager.emit('test.event', {});
      
      // Both should have been called due to snapshot
      assert.deepStrictEqual(results, ['first', 'second']);
    });
  });

  describe('has()', () => {
    it('should return true for event with subscribers', () => {
      manager.add('test.event', () => {});
      assert.strictEqual(manager.has('test.event'), true);
    });

    it('should return false for event without subscribers', () => {
      assert.strictEqual(manager.has('non.existent'), false);
    });
  });

  describe('getEvents()', () => {
    it('should return all registered event names', () => {
      manager.add('event1', () => {});
      manager.add('event2', () => {});
      manager.add('event3', () => {});
      
      const events = manager.getEvents();
      
      assert.strictEqual(events.length, 3);
      assert.strictEqual(events.includes('event1'), true);
      assert.strictEqual(events.includes('event2'), true);
      assert.strictEqual(events.includes('event3'), true);
    });

    it('should return empty array when no subscriptions', () => {
      const events = manager.getEvents();
      assert.deepStrictEqual(events, []);
    });
  });

  describe('getCount()', () => {
    it('should return correct count of callbacks', () => {
      manager.add('test.event', () => {});
      manager.add('test.event', () => {});
      
      assert.strictEqual(manager.getCount('test.event'), 2);
    });

    it('should return 0 for non-existent event', () => {
      assert.strictEqual(manager.getCount('non.existent'), 0);
    });
  });

  describe('clear()', () => {
    it('should remove all subscriptions', () => {
      manager.add('event1', () => {});
      manager.add('event2', () => {});
      manager.clear();
      
      assert.strictEqual(manager.getEvents().length, 0);
      assert.strictEqual(manager.has('event1'), false);
      assert.strictEqual(manager.has('event2'), false);
    });
  });
});

