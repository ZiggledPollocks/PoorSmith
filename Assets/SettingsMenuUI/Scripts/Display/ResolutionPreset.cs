namespace SettingsMenuUI
{
    public enum ResolutionPresetType
    {
        QHD,
        FHD,
        HD
    }

    public readonly struct ResolutionPreset
    {
        public ResolutionPresetType Type { get; }
        public int Width { get; }
        public int Height { get; }

        public string DisplayName => $"{Width} × {Height}";

        public ResolutionPreset(ResolutionPresetType type, int width, int height)
        {
            Type = type;
            Width = width;
            Height = height;
        }

        public bool Matches(int width, int height)
        {
            return Width == width && Height == height;
        }
    }
}
