// [코드 지도] ResolutionPreset: 선택 가능한 화면 해상도 데이터를 보관한다.
// 주요 함수: ResolutionPreset, Matches, DisplayName
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/SettingsMenuUI/Scripts/Display/ResolutionPreset.cs.md

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
