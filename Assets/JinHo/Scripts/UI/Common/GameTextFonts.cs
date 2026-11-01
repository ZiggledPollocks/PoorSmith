using TMPro;
using UnityEngine;

/// <summary>Shared project-owned references; text receives its font when it is created.</summary>
public sealed class GameTextFonts : ScriptableObject
{
    public TMP_FontAsset primary;
    public Font legacy;
    static GameTextFonts current;
    static GameTextFonts Current => current != null ? current : current = Resources.Load<GameTextFonts>("Fonts/GameTextFonts");
    public static TMP_FontAsset Primary => Current != null && Current.primary != null ? Current.primary : TMP_Settings.defaultFontAsset;
    public static Font Legacy => Current != null ? Current.legacy : null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => current = null;
}
