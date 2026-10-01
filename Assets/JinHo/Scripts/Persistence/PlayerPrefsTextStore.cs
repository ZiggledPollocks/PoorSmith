// [코드 지도] PlayerPrefsTextStore: ITextStore를 Unity PlayerPrefs의 문자열 키로 구현한다. Write는 SetString, Flush는 Save로 분리되어 있다.
// 주요 함수: PlayerPrefsTextStore, Exists, Read
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Persistence/PlayerPrefsTextStore.cs.md

using UnityEngine;

/// <summary>Stores text through Unity PlayerPrefs for the ITextStore contract.</summary>
public sealed class PlayerPrefsTextStore : ITextStore
{
    private readonly string key;
    public PlayerPrefsTextStore(string key) => this.key = key;
    public bool Exists => PlayerPrefs.HasKey(key);
    public string Read() => PlayerPrefs.GetString(key, string.Empty);
    public void Write(string text) => PlayerPrefs.SetString(key, text);
    public void Flush() => PlayerPrefs.Save();
}
