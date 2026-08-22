using System;
using System.Collections.Generic;

namespace OCIDE.Extensibility
{
    public static class EventAggregator
    {
        private static readonly Dictionary<string, List<Action<object>>> _subscribers = new Dictionary<string, List<Action<object>>>();

        public static void Subscribe(string eventName, Action<object> action)
        {
            if (!_subscribers.ContainsKey(eventName))
            {
                _subscribers[eventName] = new List<Action<object>>();
            }
            _subscribers[eventName].Add(action);
        }

        public static void Publish(string eventName, object payload = null)
        {
            if (_subscribers.TryGetValue(eventName, out var actions))
            {
                foreach (var action in actions)
                {
                    try
                    {
                        action(payload);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in event {eventName}: {ex.Message}");
                    }
                }
            }
        }
    }
}
