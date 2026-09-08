public interface IResourceProvider
{
    ResourceData ResourceData { get; }
    int Tier { get; }
    int AddItemInterval { get; set; }
    int MaxInteractCount { get; }
}
