using AntiPrism.Core.Models;
using AntiPrism.Core.Storage;
using Xunit;

namespace AntiPrism.Tests;

public class BlacklistStoreTests
{
    [Fact]
    public void IsBlacklisted_MatchesByFullPath()
    {
        var store = new BlacklistStore();
        store.AddOrUpdate(new BlacklistEntry
        {
            ExePath = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
            FileName = "obs64.exe",
            Mode = BlacklistMode.KillWin32,
            IsEnabled = true
        });

        bool isHit = store.IsBlacklisted(
            @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
            "obs64.exe",
            out var matched);

        Assert.True(isHit);
        Assert.NotNull(matched);
        Assert.Equal("obs64.exe", matched!.FileName);
    }

    [Fact]
    public void IsBlacklisted_MatchesByFileNameCaseInsensitive()
    {
        var store = new BlacklistStore();
        store.AddOrUpdate(new BlacklistEntry
        {
            FileName = "notepad.exe",
            Mode = BlacklistMode.KillWin32,
            IsEnabled = true
        });

        bool isHit = store.IsBlacklisted(
            null,
            "NOTEPAD.EXE",
            out var matched);

        Assert.True(isHit);
        Assert.NotNull(matched);
    }

    [Fact]
    public void IsBlacklisted_IgnoresDisabledEntries()
    {
        var store = new BlacklistStore();
        store.AddOrUpdate(new BlacklistEntry
        {
            FileName = "zoom.exe",
            IsEnabled = false
        });

        bool isHit = store.IsBlacklisted(
            @"C:\Users\User\AppData\Roaming\Zoom\bin\zoom.exe",
            "zoom.exe",
            out var matched);

        Assert.False(isHit);
        Assert.Null(matched);
    }
}
