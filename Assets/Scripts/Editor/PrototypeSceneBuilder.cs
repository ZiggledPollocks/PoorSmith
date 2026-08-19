using PoorSmith.Data;
using PoorSmith.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PoorSmith.Editor
{
    /// <summary>
    /// 프로토타입 씬을 통째로 만들어준다.
    /// 손으로 캔버스를 깔고 컴포넌트를 붙이는 과정을 없애, 화면 구조가 바뀌어도 메뉴 한 번으로 다시 만든다.
    /// </summary>
    internal static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/NodePrototype.unity";

        [MenuItem("PoorSmith/프로토타입 씬 만들기")]
        internal static void Build()
        {
            var items = Load<ItemDatabase>("Assets/Content/ItemDatabase.asset");
            var nodes = Load<NodeDatabase>("Assets/Content/NodeDatabase.asset");

            if (items == null || nodes == null)
            {
                EditorUtility.DisplayDialog(
                    "데이터가 없습니다",
                    "먼저 'PoorSmith → 노션 데이터 다시 불러오기'를 실행해주세요.",
                    "확인");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            CreateCanvas(out var canvas);
            CreateEventSystem();

            var root = new GameObject("PrototypeRoot", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var prototype = root.AddComponent<PrototypeRoot>();
            prototype.Bind(items, nodes);
            EditorUtility.SetDirty(prototype);

            NotionSourceFiles.EnsureFolder("Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[프로토타입] 씬을 만들었습니다 — {ScenePath}. 재생 버튼을 누르면 실행됩니다.");
        }

        static void CreateCanvas(out Canvas canvas)
        {
            var go = new GameObject("Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
        }

        static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();

            // 이 프로젝트는 새 Input System을 쓰므로 옛 입력 모듈을 붙이면 실행 중에 예외가 난다.
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
