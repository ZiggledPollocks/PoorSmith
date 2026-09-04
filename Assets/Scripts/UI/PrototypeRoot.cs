using System.Linq;
using PoorSmith.Crafting;
using PoorSmith.Data;
using UnityEngine;

namespace PoorSmith.UI
{
    /// <summary>
    /// 프로토타입 전체를 조립하는 곳. 씬에는 이 컴포넌트 하나만 있으면 된다.
    ///
    /// 화면을 좌우로 나눠 지도를 늘 보이게 두는 것이 이번 구조의 핵심이다.
    /// 탭으로 감췄더니 잘 안 보게 되더라는 것이 이전 프로토타입의 결론이었다.
    /// </summary>
    public sealed class PrototypeRoot : MonoBehaviour
    {
        [SerializeField] ItemDatabase items;
        [SerializeField] NodeDatabase nodes;

        [Tooltip("노드 지도에 쓸 도형. 비워두면 코드로 그린 임시 도형을 쓴다.")]
        [SerializeField] NodeVisuals visuals;

        [Tooltip("시작할 때 채집 자원을 조금 쥐여준다. 임시 조치다.")]
        [SerializeField] bool giveStartingItems = true;

        NodeGraph graph;
        NodeProgress progress;
        NodeVisibility visibility;
        Inventory inventory;
        CraftingService crafting;

        NodeMapView map;
        NodeDetailPanel detail;
        CraftPanel craft;
        DebugPanel debug;

        /// <summary>씬을 만들 때 데이터베이스를 물려준다.</summary>
        public void Bind(ItemDatabase itemDatabase, NodeDatabase nodeDatabase)
        {
            items = itemDatabase;
            nodes = nodeDatabase;
        }

        void Start()
        {
            ResolveMissingDatabases();

            if (items == null || nodes == null)
            {
                Debug.LogError(
                    "[프로토타입] 아이템/노드 데이터베이스가 지정되지 않았습니다. " +
                    "'PoorSmith → 노션 데이터 다시 불러오기'를 먼저 실행했는지 확인해주세요.", this);
                return;
            }

            graph = new NodeGraph(nodes.Nodes);
            foreach (var problem in graph.Problems)
                Debug.LogWarning($"[노드 데이터] {problem}");

            progress = new NodeProgress();
            visibility = new NodeVisibility(graph, progress);
            inventory = new Inventory();
            crafting = new CraftingService(items, graph, progress, inventory);

            UIFactory.UseVisuals(visuals);
            BuildScreen();

            // 저장된 게 있으면 이어서 하고, 없을 때만 새 판을 차린다.
            if (!SaveService.Load(progress, inventory, nodes, items) && giveStartingItems)
            {
                foreach (var item in items.Items.Where(i => i.IsGathered && !i.IsFailureResult))
                    inventory.Add(item, 5);
            }

            crafting.UnlockReachableStartNodes();
            RefreshAll();
        }

        void BuildScreen()
        {
            var canvas = UIFactory.Rect("Screen", transform);
            UIFactory.Stretch(canvas);

            // 왼쪽 지도, 오른쪽 위 설명창, 오른쪽 아래 작업 공간, 맨 아래 디버그 줄.
            map = NodeMapView.Create(Region(canvas, 0f, 0.09f, 0.58f, 1f), graph, progress, visibility);
            detail = NodeDetailPanel.Create(Region(canvas, 0.58f, 0.55f, 1f, 1f), progress, visibility);
            craft = CraftPanel.Create(Region(canvas, 0.58f, 0.09f, 1f, 0.55f), items, inventory, crafting);
            debug = DebugPanel.Create(Region(canvas, 0f, 0f, 1f, 0.09f), items, graph, progress, inventory, crafting);

            map.SelectionChanged += _ => RefreshDetail();
            map.FocusChanged += RefreshDetail;

            detail.PrepareRequested += node => craft.PrepareFor(node);
            detail.FocusToggleRequested += ToggleFocus;

            craft.Changed += RefreshAll;
            debug.Changed += RefreshAll;
        }

        void ToggleFocus(NodeDef node)
        {
            if (map.FocusRoot != null) map.ExitFocus();
            else map.EnterFocus(node);
        }

        void RefreshAll()
        {
            map.Rebuild();
            craft.Refresh();
            debug.Refresh();
            RefreshDetail();

            SaveService.Save(progress, inventory);
        }

        // 재생을 멈추거나 창을 닫을 때도 마지막 상태를 남긴다.
        void OnApplicationQuit()
        {
            if (progress != null) SaveService.Save(progress, inventory);
        }

        void RefreshDetail() =>
            detail.Show(map.Selected, map.FocusRoot != null && map.FocusRoot == map.Selected);

        /// <summary>
        /// 씬에 참조가 비어 있어도 에디터에서는 스스로 찾아 붙인다.
        /// 씬을 손으로 만들었거나 참조가 끊겼을 때 조용히 멈추지 않게 하려는 것이다.
        /// </summary>
        void ResolveMissingDatabases()
        {
#if UNITY_EDITOR
            if (items == null)
                items = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDatabase>(
                    "Assets/Content/ItemDatabase.asset");

            if (nodes == null)
                nodes = UnityEditor.AssetDatabase.LoadAssetAtPath<NodeDatabase>(
                    "Assets/Content/NodeDatabase.asset");
#endif
        }

        /// <summary>0~1 비율로 화면의 한 구역을 잘라낸다.</summary>
        static Transform Region(Transform parent, float minX, float minY, float maxX, float maxY)
        {
            var rect = UIFactory.Rect("Region", parent);
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
