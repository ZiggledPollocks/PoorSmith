/// <summary>Marks interactables that provide gatherable resources.</summary>
public interface IResourceProvider
{
    ResourceData ResourceData { get; }
    int Tier { get; }
    int AddItemInterval { get; set; }
    int MaxInteractCount { get; }
}
