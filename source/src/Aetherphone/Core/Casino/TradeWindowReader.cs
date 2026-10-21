using System.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

namespace Aetherphone.Core.Casino;

internal sealed unsafe class TradeWindowReader
{
    public const string TradeAddonName = "Trade";

    private const int SendGilIndex = 0xF0 / sizeof(int);
    private const int ReceiveGilIndex = 0xF4 / sizeof(int);
    private const int TradePartnerNameIndex = 0x58 / 8;

    private byte[] partnerBytes = Array.Empty<byte>();
    private string partnerName = string.Empty;
    private string partnerWorld = string.Empty;

    public TradeWindowSample Sample()
    {
        var inventory = InventoryManager.Instance();
        var walletRead = inventory != null && Plugin.ClientState.IsLoggedIn;
        var walletGil = walletRead ? (long)inventory->GetGil() : 0L;
        if (!WindowVisible())
        {
            return new TradeWindowSample(false, string.Empty, string.Empty, 0, 0, walletGil, walletRead);
        }

        ReadPartner();
        var numbers = AtkStage.Instance() == null ? null : AtkStage.Instance()->GetNumberArrayData(NumberArrayType.Trade);
        var numbersReady = numbers != null && numbers->IntArray != null && numbers->Size > ReceiveGilIndex;
        var mine = numbersReady ? (uint)numbers->IntArray[SendGilIndex] : 0L;
        var theirs = numbersReady ? (uint)numbers->IntArray[ReceiveGilIndex] : 0L;
        return new TradeWindowSample(true, partnerName, partnerWorld, mine, theirs, walletGil, walletRead);
    }

    private void ReadPartner()
    {
        var strings = AtkStage.Instance() == null ? null : AtkStage.Instance()->GetStringArrayData(StringArrayType.Trade);
        var partner = strings == null || strings->StringArray == null || strings->Size <= TradePartnerNameIndex
            ? default
            : strings->StringArray[TradePartnerNameIndex];
        var raw = !partner.HasValue
            ? ReadOnlySpan<byte>.Empty
            : partner.AsSpan();
        if (raw.SequenceEqual(partnerBytes))
        {
            return;
        }

        partnerBytes = raw.ToArray();
        if (raw.IsEmpty)
        {
            partnerName = string.Empty;
            partnerWorld = string.Empty;
            return;
        }

        Split(new ReadOnlySeStringSpan(raw), out partnerName, out partnerWorld);
    }

    private static void Split(ReadOnlySeStringSpan text, out string name, out string world)
    {
        var nameText = new StringBuilder();
        var worldText = new StringBuilder();
        var pastName = false;
        foreach (var payload in text)
        {
            if (payload.Type != ReadOnlySePayloadType.Text)
            {
                pastName |= nameText.Length > 0;
                continue;
            }

            (pastName ? worldText : nameText).Append(Encoding.UTF8.GetString(payload.Body));
        }

        name = nameText.ToString().Trim();
        world = worldText.ToString().Trim();
    }

    private static bool WindowVisible()
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(TradeAddonName).Address;
        return addon != null && addon->IsVisible;
    }
}
