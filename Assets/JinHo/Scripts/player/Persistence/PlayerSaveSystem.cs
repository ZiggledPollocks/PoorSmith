using System;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerToolController))]
public sealed class PlayerSaveSystem : MonoBehaviour
{
    private const int CurrentVersion = 1;
    private const string SaveFileName = "player-state-v1.json";

    [Serializable]
    private sealed class PlayerSaveData
    {
        public int version = CurrentVersion;
        public string currentToolId = string.Empty;
        public string equippedArmorId = string.Empty;
    }

    [SerializeField] private PlayerToolController toolController;
    [SerializeField] private string equippedArmorId = string.Empty;

    public enum LoadStatus { NotLoaded, Missing, Loaded, Invalid, Unsupported, UnknownTool, IOError }
    public LoadStatus LastLoadStatus { get; private set; }
    public bool WritesBlocked => LastLoadStatus != LoadStatus.Missing && LastLoadStatus != LoadStatus.Loaded;
    private bool isLoading;
    // The integrated town/field loop owns tool selection and equipment in one
    // snapshot. Keep this component only for standalone player scenes.
    private bool ManagedBySmithingLoop => FindFirstObjectByType<SmithingLoop>() != null;
    private bool AutoSaveEnabled => FindFirstObjectByType<SettingsMenuUI.GameSettingsController>(FindObjectsInactive.Include)?.AutoSaveEnabled ?? true;
    private void SaveAutomatically() { if (!ManagedBySmithingLoop && AutoSaveEnabled) Save(); }
    private ITextStore store;
    private ITextStore Store => store ??= new FileTextStore(SavePath);

    public string CurrentToolId => toolController != null && toolController.CurrentTool != null
        ? toolController.CurrentTool.ToolId
        : string.Empty;
    public string EquippedArmorId => equippedArmorId;
    public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public event Action<string> EquippedArmorIdChanged;

    private void Awake()
    {
        if (toolController == null)
            toolController = GetComponent<PlayerToolController>();
    }

    private void OnEnable()
    {
        if (toolController != null)
            toolController.CurrentToolChanged += HandleCurrentToolChanged;
    }

    private void Start()
    {
        if (ManagedBySmithingLoop) { LastLoadStatus = LoadStatus.NotLoaded; return; }
        Load();
        if (LastLoadStatus == LoadStatus.Missing) SaveAutomatically();
    }

    private void OnDisable()
    {
        if (toolController != null)
            toolController.CurrentToolChanged -= HandleCurrentToolChanged;
    }

    public void SetEquippedArmorId(string armorId)
    {
        string normalizedId = NormalizeId(armorId);
        if (equippedArmorId == normalizedId)
            return;

        equippedArmorId = normalizedId;
        EquippedArmorIdChanged?.Invoke(equippedArmorId);
        SaveAutomatically();
    }

    public bool Save()
    {
        if (ManagedBySmithingLoop || WritesBlocked || isLoading) return false;
        var data = new PlayerSaveData
        {
            currentToolId = NormalizeId(CurrentToolId),
            equippedArmorId = NormalizeId(equippedArmorId)
        };

        try
        {
            Store.Write(JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Player save failed: {exception.Message}", this);
            return false;
        }
    }

    public bool Load()
    {
        if (ManagedBySmithingLoop) { LastLoadStatus = LoadStatus.NotLoaded; return false; }
        LastLoadStatus = LoadStatus.IOError;
        try
        {
            if (!Store.Exists) { LastLoadStatus = LoadStatus.Missing; return false; }
            string json = Store.Read();
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            if (data == null || !json.Contains("\"version\"") || data.version < 1)
            { LastLoadStatus = LoadStatus.Invalid; return false; }

            if (data.version > CurrentVersion)
            {
                LastLoadStatus = LoadStatus.Unsupported;
                Debug.LogWarning(
                    $"Player save version {data.version} is newer than supported version {CurrentVersion}.",
                    this);
                return false;
            }

            isLoading = true;
            equippedArmorId = NormalizeId(data.equippedArmorId);

            string savedToolId = NormalizeId(data.currentToolId);
            if (!string.IsNullOrEmpty(savedToolId) &&
                (toolController == null || !toolController.SelectToolId(savedToolId)))
            {
                LastLoadStatus = LoadStatus.UnknownTool;
                Debug.LogWarning(
                    $"Saved Tool ID '{savedToolId}' is not available. Keeping the default tool.",
                    this);
            }

            EquippedArmorIdChanged?.Invoke(equippedArmorId);
            if (LastLoadStatus == LoadStatus.UnknownTool) return false;
            LastLoadStatus = LoadStatus.Loaded;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Player save could not be loaded: {exception.Message}", this);
            return false;
        }
        finally
        {
            isLoading = false;
        }
    }

    private void HandleCurrentToolChanged(ToolData tool, int slotIndex)
    {
        if (!isLoading)
            SaveAutomatically();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveAutomatically();
    }

    private void OnApplicationQuit()
    {
        SaveAutomatically();
    }

    private static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}
