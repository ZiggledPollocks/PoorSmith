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
