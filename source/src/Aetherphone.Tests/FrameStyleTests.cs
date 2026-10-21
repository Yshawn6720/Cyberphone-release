using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FrameStyleTests
{
    [Fact]
    public void ScaleTurnsAPercentageIntoAMultiplier()
    {
        Assert.Equal(1.38f, FrameStyle.ScaleOf(138), 3);
        Assert.Equal(1f, FrameStyle.ScaleOf(100), 3);
    }

    [Fact]
    public void ScaleClampsWhatTheServerSends()
    {
        Assert.Equal(1f, FrameStyle.ScaleOf(10), 3);
        Assert.Equal(2f, FrameStyle.ScaleOf(9000), 3);
        Assert.Equal(1f, FrameStyle.ScaleOf(0), 3);
        Assert.Equal(1f, FrameStyle.ScaleOf(-40), 3);
    }

    [Fact]
    public void ADescriptorBecomesAStyleWithItsOwnScale()
    {
        var style = FrameStyle.From(new FrameDescriptorDto("frame-a", "Ember Halo", "a.png",
            "https://cdn.example/frame/a.png", 150));

        Assert.Equal("frame-a", style.Id);
        Assert.Equal("Ember Halo", style.Name);
        Assert.Equal(1.5f, style.Scale, 3);
        Assert.Equal("https://cdn.example/frame/a.png", style.AssetUrl);
    }

    [Fact]
    public void ThePayloadTheBackendSendsDeserialisesIntoTheCatalog()
    {
        const string payload = """
        {
          "frames": [
            {
              "id": "shop-frame-ember",
              "name": "Ember Halo",
              "asset": "shop-frame-ember/abc.png",
              "assetUrl": "https://cdn.example/frame/shop-frame-ember/abc.png",
              "scalePercent": 142,
              "translations": [{ "lang": "fr", "name": "Halo de braise" }]
            }
          ]
        }
        """;

        var catalog = JsonSerializer.Deserialize(payload, AethernetJsonContext.Default.FrameCatalogDto);

        Assert.NotNull(catalog);
        Assert.Single(catalog!.Frames);
        var style = FrameStyle.From(catalog.Frames[0]);
        Assert.Equal("shop-frame-ember", style.Id);
        Assert.Equal(1.42f, style.Scale, 3);
    }

    [Fact]
    public void TheInventoryPayloadKeepsBothKindsApart()
    {
        const string payload = """
        {
          "sections": [
            {
              "kind": "flair",
              "slots": 2,
              "items": [
                { "id": "b1", "kind": "flair", "slot": 1, "locked": false,
                  "badge": { "id": "b1", "name": "Emberling", "icon": "0xF06D" } }
              ]
            },
            {
              "kind": "frame",
              "slots": 1,
              "items": [
                { "id": "f1", "kind": "frame", "slot": 0, "locked": false,
                  "frame": { "id": "f1", "name": "Ember Halo", "scalePercent": 138 } }
              ]
            }
          ]
        }
        """;

        var inventory = JsonSerializer.Deserialize(payload, AethernetJsonContext.Default.InventoryDto);

        Assert.NotNull(inventory);
        Assert.Equal(2, inventory!.Sections.Length);

        var badges = inventory.Sections[0];
        Assert.Equal(LoadoutStore.BadgeKind, badges.Kind);
        Assert.Equal(2, badges.Slots);
        Assert.Equal(1, badges.Items[0].Slot);
        Assert.NotNull(badges.Items[0].Badge);
        Assert.Null(badges.Items[0].Frame);

        var frames = inventory.Sections[1];
        Assert.Equal(LoadoutStore.FrameKind, frames.Kind);
        Assert.Equal(1, frames.Slots);
        Assert.Equal(0, frames.Items[0].Slot);
        Assert.NotNull(frames.Items[0].Frame);
    }

    [Fact]
    public void TheKindConstantsMatchTheBackendSkuKinds()
    {
        Assert.Equal("flair", LoadoutStore.BadgeKind);
        Assert.Equal("frame", LoadoutStore.FrameKind);
    }
}
