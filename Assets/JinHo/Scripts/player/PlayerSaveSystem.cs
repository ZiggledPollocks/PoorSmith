using System;
using System.IO;
using System.Text;
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

    private bool isLoading;

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
        if (!Load())
            Save();
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
        Save();
    }

    public bool Save()
    {
        var data = new PlayerSaveData
        {
            currentToolId = NormalizeId(CurrentToolId),
            equippedArmorId = NormalizeId(equippedArmorId)
        };

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporaryPath = SavePath + ".tmp";
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Copy(temporaryPath, SavePath, true);
            File.Delete(temporaryPath);
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
        if (!File.Exists(SavePath))
            return false;

        try
        {
            string json = File.ReadAllText(SavePath, Encoding.UTF8);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            if (data == null)
                return false;

            if (data.version > CurrentVersion)
            {
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
                Debug.LogWarning(
                    $"Saved Tool ID '{savedToolId}' is not available. Keeping the default tool.",
                    this);
            }

            EquippedArmorIdChanged?.Invoke(equippedArmorId);
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
            Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}
