using System;
using PoorSmith.Crafting;
using PoorSmith.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PoorSmith.UI
{
    /// <summary>
    /// 선택한 노드의 설명창. 기획서의 '두루마리' 자리다.
    /// 미해금이면 힌트만, 해금이면 제작법을 보여준다.
    ///
    /// '이 레시피로 작업 준비' 버튼이 피드백 3·7번 — "레시피를 계속 확인하며 제작하는 과정이 답답",
    /// "찾아낸 레시피는 미리 세팅해 줄 필요" — 에 대한 대응이다.
    /// </summary>
    internal sealed class NodeDetailPanel : MonoBehaviour
    {
        NodeProgress progress;
        NodeVisibility visibility;

        Text title;
        Text body;
        Button prepareButton;
        Button focusButton;
        Text focusLabel;

        NodeDef current;

        internal event Action<NodeDef> PrepareRequested;
        internal event Action<NodeDef> FocusToggleRequested;

        internal static NodeDetailPanel Create(Transform parent, NodeProgress progress, NodeVisibility visibility)
        {
            var root = UIFactory.Rect("NodeDetail", parent);
            UIFactory.Stretch(root);
            var panel = root.gameObject.AddComponent<NodeDetailPanel>();
            panel.progress = progress;
            panel.visibility = visibility;
            panel.Build(root);
            panel.Show(null);
            return panel;
        }

        void Build(RectTransform root)
        {
            var background = UIFactory.Panel("Background", root, UITheme.ScrollBackground);
            UIFactory.Stretch(background.rectTransform);

            var column = UIFactory.ScrollColumn(root);

            title = UIFactory.Label("Title", column, "", UITheme.TitleSize, UITheme.Title, TextAnchor.UpperLeft);
            body = UIFactory.Label("Body", column, "", UITheme.BodySize, UITheme.Body, TextAnchor.UpperLeft);

            prepareButton = UIFactory.Button("Prepare", column, "이 레시피로 작업 준비",
                UITheme.ScrollFace, UITheme.Title);
            UIFactory.FixHeight(prepareButton, UITheme.ActionHeight);
            prepareButton.onClick.AddListener(() => PrepareRequested?.Invoke(current));

            focusButton = UIFactory.Button("Focus", column, "파생 집중 모드",
                UITheme.ButtonFace, UITheme.ButtonText);
            UIFactory.FixHeight(focusButton, UITheme.RowHeight);
            focusButton.onClick.AddListener(() => FocusToggleRequested?.Invoke(current));
            focusLabel = focusButton.GetComponentInChildren<Text>();
        }

        internal void Show(NodeDef node, bool focused = false)
        {
            current = node;

            if (node == null)
            {
                title.text = "노드를 고르세요";
                body.text = "왼쪽 지도에서 노드를 클릭하면 여기에 내용이 나옵니다.";
                prepareButton.gameObject.SetActive(false);
                focusButton.gameObject.SetActive(false);
                return;
            }

            var unlocked = progress.IsUnlocked(node);

            title.text = unlocked ? node.DisplayName : "아직 모르는 것";
            body.text = DescribeFor(node, unlocked);

            // 이미 찾아낸 레시피만 미리 채워준다. 미해금 노드까지 채워주면
            // 시행착오로 찾아내는 과정 자체가 사라진다. 시작 노드는 만들 대상이 없다.
            prepareButton.gameObject.SetActive(unlocked && node.Recipe != null);

            var canFocus = visibility.CanFocus(node);
            focusButton.gameObject.SetActive(canFocus || focused);
            focusLabel.text = focused ? "지도 전체로 돌아가기" : "파생 집중 모드";
        }

        static string DescribeFor(NodeDef node, bool unlocked)
        {
            if (node.Type == NodeType.Start)
                return unlocked
                    ? node.Description
                    : $"{Name(node.StartResource)}을(를) 손에 넣으면 열린다.";

            if (!unlocked)
                return string.IsNullOrWhiteSpace(node.Hint)
                    ? "아직 아무런 실마리가 없다."
                    : node.Hint;

            var recipe = node.Recipe != null ? node.Recipe.Recipe.Describe() : "제작법 없음";
            return string.IsNullOrWhiteSpace(node.Description)
                ? $"제작법 — {recipe}"
                : $"제작법 — {recipe}\n\n{node.Description}";
        }

        static string Name(ItemDef item) => item != null ? item.DisplayName : "무언가";
    }
}
