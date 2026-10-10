using AntiPrism.Core.Models;
using AntiPrism.Tray.Gesture;
using Xunit;

namespace AntiPrism.Tests;

public class GestureTrackerTests
{
    [Fact]
    public void SixHitsWithinWindow_TriggersGesture()
    {
        var tracker = new GestureTracker();
        var config = new GestureConfig
        {
            SecretValue = 61.0,
            SecretCount = 6,
            SecretWindowSec = 10
        };

        bool triggered = false;
        tracker.GestureTriggered += () => triggered = true;

        // Perform 6 crossings into 61
        for (int i = 0; i < 6; i++)
        {
            tracker.OnSliderValueChanged(60.0, 61.0, config);
        }

        Assert.True(triggered);
    }

    [Fact]
    public void FiveHits_DoesNotTrigger()
    {
        var tracker = new GestureTracker();
        var config = new GestureConfig
        {
            SecretValue = 61.0,
            SecretCount = 6,
            SecretWindowSec = 10
        };

        bool triggered = false;
        tracker.GestureTriggered += () => triggered = true;

        for (int i = 0; i < 5; i++)
        {
            tracker.OnSliderValueChanged(60.0, 61.0, config);
        }

        Assert.False(triggered);
    }

    [Fact]
    public void StayingAt61_DoesNotCountAsMultipleCrossings()
    {
        var tracker = new GestureTracker();
        var config = new GestureConfig
        {
            SecretValue = 61.0,
            SecretCount = 6,
            SecretWindowSec = 10
        };

        bool triggered = false;
        tracker.GestureTriggered += () => triggered = true;

        tracker.OnSliderValueChanged(60.0, 61.0, config); // 1st crossing
        for (int i = 0; i < 10; i++)
        {
            tracker.OnSliderValueChanged(61.0, 61.0, config); // Same value, not crossing
        }

        Assert.False(triggered);
    }

    [Fact]
    public void MovingBetweenOtherValues_DoesNotCount()
    {
        var tracker = new GestureTracker();
        var config = new GestureConfig
        {
            SecretValue = 61.0,
            SecretCount = 6,
            SecretWindowSec = 10
        };

        bool triggered = false;
        tracker.GestureTriggered += () => triggered = true;

        for (int i = 0; i < 20; i++)
        {
            tracker.OnSliderValueChanged(59.0, 60.0, config);
            tracker.OnSliderValueChanged(60.0, 62.0, config);
        }

        Assert.False(triggered);
    }

    [Fact]
    public void Reset_ClearsAccumulatedHits()
    {
        var tracker = new GestureTracker();
        var config = new GestureConfig
        {
            SecretValue = 61.0,
            SecretCount = 6,
            SecretWindowSec = 10
        };

        bool triggered = false;
        tracker.GestureTriggered += () => triggered = true;

        for (int i = 0; i < 5; i++)
        {
            tracker.OnSliderValueChanged(60.0, 61.0, config);
        }

        tracker.Reset();

        // 1 more crossing should not trigger since previous 5 were reset
        tracker.OnSliderValueChanged(60.0, 61.0, config);

        Assert.False(triggered);
    }
}
