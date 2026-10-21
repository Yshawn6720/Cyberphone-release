namespace Aetherphone.Core.Shell;

[Serializable]
internal sealed class MinimizedLayout
{
    public List<MinimizedLayoutItem> Items { get; set; } = new();
}

[Serializable]
internal sealed class MinimizedLayoutItem
{
    public string PartId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
