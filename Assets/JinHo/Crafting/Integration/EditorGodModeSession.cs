// [코드 지도] EditorGodModeSession: 에디터 전용 God 모드 지급과 일반 저장과 분리된 임시 플레이 세션을 관리한다.
// 주요 함수: TryGrant, ReportUncataloguedTools, BeginIsolatedSession
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Integration/EditorGodModeSession.cs.md

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blacksmith;
using UnityEditor;
using UnityEngine;

// This entire file is absent from player builds. No scene or prefab serializes it.
[InitializeOnLoad]
public static class EditorGodModeSession
{
    const string RootKey = "batterMap.EditorGodMode.Root";
    const string GrantedKey = "batterMap.EditorGodMode.Granted";

    static EditorGodModeSession()
    {
        // Recreate only the unsaved button after a script/domain reload in Play Mode.
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying) EditorGodModeButton.Install();
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.EraseString(RootKey);
                SessionState.EraseBool(GrantedKey);
            }
        };
    }

    public static string ActiveRoot => SessionState.GetString(RootKey, string.Empty);
    public static bool IsActive => !string.IsNullOrEmpty(ActiveRoot);
    public static bool Granted => SessionState.GetBool(GrantedKey, false);

    // 핵심 분기: Granted 판정.
    // 상태 변경: message 갱신.
    // 다음 연결: Blacksmith.InventoryService.Add(System.Collections.Generic.List<Blacksmith.Stack>, Blacksmith.Stack) 호출.
    public static bool TryGrant(SmithingLoop loop, out string message)
    {
        message = string.Empty;
        if (Granted) { message = "이미 이 플레이 세션에 지급했습니다."; return false; }
        if (loop == null || !loop.Initialized || loop.Catalog == null || !loop.CanSnapshot)
        { message = "게임과 카탈로그가 준비될 때까지 기다려 주세요."; return false; }

        try
        {
            BlacksmithCatalog catalog = loop.Catalog;
            SaveData source = loop.EditorGrantSource;
            if (catalog.items == null || catalog.recipes == null || source == null ||
                source.chest == null || source.bag == null || source.equipment == null ||
                source.progress == null || source.acquiredItems == null)
                throw new InvalidDataException("카탈로그 또는 인벤토리 목록이 비어 있습니다.");

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            int toolTypes = 0;
            foreach (ItemDefinition item in catalog.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id))
                    throw new InvalidDataException("ID가 없는 카탈로그 아이템이 있습니다.");
                if (!itemIds.Add(item.id)) continue;
                if (item.toolTier > 0 || !string.IsNullOrWhiteSpace(item.toolKind)) toolTypes++;
            }
            if (itemIds.Count == 0) throw new InvalidDataException("지급할 카탈로그 아이템이 없습니다.");
            foreach (Stack stack in source.chest.Concat(source.bag))
                if (stack == null || stack.count <= 0 || !itemIds.Contains(stack.itemId) ||
                    (int)stack.quality < 0 || (int)stack.quality > (int)Quality.Master)
                    throw new InvalidDataException("기존 인벤토리에 잘못된 스택이 있습니다.");
            foreach (EquipmentEntry equipment in source.equipment)
                if (equipment?.stack == null || equipment.stack.count <= 0 ||
                    !itemIds.Contains(equipment.stack.itemId))
                    throw new InvalidDataException("장착 장비 데이터가 올바르지 않습니다.");
            if (source.progress.Any(p => p == null || string.IsNullOrWhiteSpace(p.id) || p.crafts < 0))
                throw new InvalidDataException("기존 레시피 진행 데이터가 올바르지 않습니다.");

            var enabledRecipes = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            foreach (RecipeDefinition recipe in catalog.recipes)
            {
                if (recipe == null) throw new InvalidDataException("비어 있는 레시피 항목이 있습니다.");
                if (!recipe.enabled) continue;
                if (string.IsNullOrWhiteSpace(recipe.id) || !itemIds.Contains(recipe.outputId) ||
                    recipe.ingredients == null || recipe.ingredients.Any(i => i == null ||
                        i.count <= 0 || !itemIds.Contains(i.itemId)) ||
                    !enabledRecipes.TryAdd(recipe.id, recipe))
                    throw new InvalidDataException("활성 레시피 ID, 산출물 또는 재료가 올바르지 않습니다.");
            }

            // Build the full result on an independent snapshot. Any stack overflow or
            // malformed recipe aborts before the live inventory or save route changes.
            SaveData staged = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(source));
            foreach (string id in itemIds)
                InventoryService.Add(staged.chest, new Stack(id, 99, Quality.High));
            new InventoryService(staged, catalog).Notify();
            foreach (string id in enabledRecipes.Keys)
            {
                RecipeProgress progress = staged.progress.FirstOrDefault(p => p.id == id);
                if (progress == null)
                    staged.progress.Add(new RecipeProgress { id = id, crafts = 1 });
                else if (progress.crafts < 1)
                    progress.crafts = 1;
            }

            int outsideTools = ReportUncataloguedTools(itemIds);
            bool startedHere = !IsActive;
            if (startedHere) BeginIsolatedSession();
            if (!loop.ApplyEditorGrant(staged, out string error))
            {
                if (startedHere) SessionState.EraseString(RootKey);
                throw new InvalidOperationException(error);
            }
            SessionState.SetBool(GrantedKey, true);
            message = $"God Mode: 아이템 {itemIds.Count}종(도구 {toolTypes}종 포함) ×99, 레시피 {enabledRecipes.Count}개" +
                (outsideTools > 0 ? $" · 카탈로그 외 도구 {outsideTools}종 미지급" : string.Empty);
            return true;
        }
        catch (Exception error)
        {
            message = "God Mode 실패: " + error.Message;
            Debug.LogError(message);
            return false;
        }
    }

    static void BeginIsolatedSession()
    {
        string ordinary = Path.Combine(Application.persistentDataPath, "EditorPlayMode");
        string isolated = Path.Combine(Application.temporaryCachePath,
            "batterMap-god-mode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        if (Directory.Exists(ordinary))
        {
            foreach (string file in Directory.GetFiles(ordinary, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(ordinary.Length).TrimStart(Path.DirectorySeparatorChar);
                string destination = Path.Combine(isolated, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }
        }
        // Every GameSavePaths consumer now sees the copy. The ordinary directory is
        // never written after this point, even if Play Mode ends without callbacks.
        SessionState.SetString(RootKey, isolated);
    }

    // 핵심 분기: tools != null 판정.
    // 다음 연결: PlayerToolController.GetToolAtSlot(int) 호출.
    static int ReportUncataloguedTools(HashSet<string> itemIds)
    {
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var tools = UnityEngine.Object.FindFirstObjectByType<PlayerToolController>();
        if (tools != null)
        {
            for (int i = 0; i < tools.ToolSlotCount; i++)
            {
                ToolData tool = tools.GetToolAtSlot(i);
                if (tool != null && !itemIds.Contains(tool.ToolId))
                    missing.Add(tool.name + " (" + tool.ToolId + ")");
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ToolData"))
        {
            ToolData tool = AssetDatabase.LoadAssetAtPath<ToolData>(AssetDatabase.GUIDToAssetPath(guid));
            if (tool != null && !itemIds.Contains(tool.ToolId))
                missing.Add(tool.name + " (" + tool.ToolId + ")");
        }
        if (missing.Count > 0)
            Debug.LogWarning("God Mode: 카탈로그 밖 플레이 가능 도구는 상자에 지급할 수 없음: " +
                string.Join(", ", missing));
        return missing.Count;
    }
}

public sealed class EditorGodModeButton : MonoBehaviour
{
    string notice;
    float noticeUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    internal static void Install()
    {
        if (FindFirstObjectByType<EditorGodModeButton>() != null) return;
        var host = new GameObject("Editor God Mode Button");
        // Runtime-created and never serialized; keep it discoverable across scenes.
        DontDestroyOnLoad(host);
        host.AddComponent<EditorGodModeButton>();
    }

    void OnGUI()
    {
        SmithingLoop loop = SmithingLoop.Instance;
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == "TitleScene") return;
        float x = Screen.width - 139f;
        float y = Screen.height * .43f;
        GUI.enabled = loop != null && loop.Initialized && loop.Catalog != null &&
            loop.CanSnapshot && !EditorGodModeSession.Granted;
        if (GUI.Button(new Rect(x, y, 130f, 35f), "God Mode"))
        {
            EditorGodModeSession.TryGrant(loop, out notice);
            noticeUntil = Time.realtimeSinceStartup + 7f;
        }
        GUI.enabled = true;
        if (Time.realtimeSinceStartup < noticeUntil && !string.IsNullOrEmpty(notice))
            GUI.Box(new Rect(Mathf.Max(4f, x - 310f), y + 39f, 440f, 42f), notice);
    }
}
#endif
