using AntiPrism.Core.Models;
using Serilog;

namespace AntiPrism.Tray.Gesture;

public class GestureTracker
{
    private readonly Queue<DateTime> _hits = new();
    private double _previousValue = -1;
    private readonly object _lock = new();

    public event Action? GestureTriggered;

    public void OnSliderValueChanged(double oldValue, double newValue, GestureConfig config)
    {
        lock (_lock)
        {
            // Detect crossing into target value (e.g. entering 61 from <61 or >61)
            bool isEnteringTarget = Math.Abs(newValue - config.SecretValue) < 0.5 &&
                                    Math.Abs(oldValue - config.SecretValue) >= 0.5;

            if (isEnteringTarget)
            {
                var now = DateTime.UtcNow;
                _hits.Enqueue(now);

                // Evict hits older than window seconds
                while (_hits.Count > 0 && (now - _hits.Peek()).TotalSeconds > config.SecretWindowSec)
                {
                    _hits.Dequeue();
                }

                Log.Debug("Secret gesture hit registered! Total in window: {Count}/{Target}",
                    _hits.Count, config.SecretCount);

                if (_hits.Count >= config.SecretCount)
                {
                    Log.Information("Secret gesture pattern matched! Triggering real panel.");
                    _hits.Clear();
                    GestureTriggered?.Invoke();
                }
            }

            _previousValue = newValue;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _hits.Clear();
            _previousValue = -1;
        }
    }
}
