// [코드 지도] PlayerSaveSystem: 현재 도구 ID와 방어구 ID를 JSON 파일에 보관한다. 소유 도구 목록·인벤토리·위치·체력의 전체 세이브가 아니다. PlayerToolLoadout이 먼저 준비한 목록에서 저장된 도구를 선택하며 PlayerToolController의 선택 이벤트에 자동 저장을 연결한다.
// 주요 함수: Load, Save, SetEquippedArmorId
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Persistence/PlayerSaveSystem.cs.md

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
    private string storePath;
    private ITextStore Store
    {
        get
        {
            string path = SavePath;
            if (store == null || storePath != path)
            {
                store = new FileTextStore(path);
                storePath = path;
            }
            return store;
        }
    }

    public string CurrentToolId => toolController != null && toolController.CurrentTool != null
        ? toolController.CurrentTool.ToolId
        : string.Empty;
    public string EquippedArmorId => equippedArmorId;
    public string SavePath => GameSavePaths.File(SaveFileName);

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

    // 핵심 분기: ManagedBySmithingLoop 판정.
    // 상태 변경: LastLoadStatus 갱신.
    // 다음 연결: ITextStore.Read() 호출.
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
