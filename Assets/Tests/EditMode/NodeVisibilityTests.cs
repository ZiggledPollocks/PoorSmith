using System.Linq;
using NUnit.Framework;
using PoorSmith.Data;
using static PoorSmith.Crafting.Tests.NodeBuilder;

namespace PoorSmith.Crafting.Tests
{
    /// <summary>
    /// 노드 지도에 무엇이 보이는지에 대한 규칙.
    /// 출처는 노션 '(NEW) 레시피 노드' 문서.
    /// </summary>
    public sealed class NodeVisibilityTests
    {
        static (NodeVisibility visibility, NodeProgress progress) Setup(params NodeDef[] nodes)
        {
            var graph = new NodeGraph(nodes);
            var progress = new NodeProgress();
            return (new NodeVisibility(graph, progress), progress);
        }

        [Test]
        public void 아무것도_해금되지_않아도_시작_노드는_보인다()
        {
            var start = Node("iron", NodeType.Start);
            var (visibility, _) = Setup(start);

            Assert.IsTrue(visibility.IsVisible(start), "첫 화면에서 지도가 비어 있으면 안 된다.");
        }

        [Test]
        public void 해금된_노드의_한_칸_앞까지만_보인다()
        {
            var start = Node("start", NodeType.Start);
            var child = Node("child", NodeType.Basic, start);
            var grandchild = Node("grandchild", NodeType.Basic, child);

            var (visibility, progress) = Setup(start, child, grandchild);
            progress.Unlock(start);

            Assert.IsTrue(visibility.IsVisible(child), "해금된 노드의 바로 다음은 보여야 한다.");
            Assert.IsFalse(visibility.IsVisible(grandchild), "두 칸 앞은 아직 가려져 있어야 한다.");
        }

        [Test]
        public void 해금하면_다음_노드가_새로_나타난다()
        {
            var start = Node("start", NodeType.Start);
            var child = Node("child", NodeType.Basic, start);
            var grandchild = Node("grandchild", NodeType.Basic, child);

            var (visibility, progress) = Setup(start, child, grandchild);
            progress.Unlock(start);
            progress.Unlock(child);

            Assert.IsTrue(visibility.IsVisible(grandchild));
        }

        [Test]
        public void 파생_노드의_하위_기본_노드는_기본_지도에서_감춰진다()
        {
            var start = Node("start", NodeType.Start);
            var derivative = Node("derivative", NodeType.Derivative, start);
            var inside = Node("inside", NodeType.Basic, derivative);

            var (visibility, progress) = Setup(start, derivative, inside);
            progress.Unlock(start);
            progress.Unlock(derivative);

            Assert.IsTrue(visibility.IsVisible(derivative), "파생 노드 자체는 보인다.");
            Assert.IsFalse(visibility.IsVisible(inside), "파생 아래 기본 노드는 기본 지도에 없다.");
        }

        [Test]
        public void 파생_노드의_하위라도_파생_노드는_뼈대로_남는다()
        {
            var start = Node("start", NodeType.Start);
            var outer = Node("outer", NodeType.Derivative, start);
            var inner = Node("inner", NodeType.Derivative, outer);

            var (visibility, progress) = Setup(start, outer, inner);
            progress.Unlock(start);
            progress.Unlock(outer);

            Assert.IsTrue(visibility.IsVisible(inner));
        }

        [Test]
        public void 혼합_노드도_하위를_격리한다()
        {
            var start = Node("start", NodeType.Start);
            var other = Node("other", NodeType.Basic, start);
            var mixed = Node("mixed", NodeType.Mixed, start, other);
            var inside = Node("inside", NodeType.Basic, mixed);

            var (visibility, progress) = Setup(start, other, mixed, inside);
            progress.Unlock(start);
            progress.Unlock(other);
            progress.Unlock(mixed);

            Assert.IsTrue(visibility.IsVisible(mixed));
            Assert.IsFalse(visibility.IsVisible(inside), "혼합 노드는 파생 노드와 같은 규칙을 따른다.");
        }

        [Test]
        public void 히든_노드는_해금하기_전까지_지도에_없다()
        {
            var start = Node("start", NodeType.Start);
            var hidden = Node("hidden", NodeType.Hidden, start);

            var (visibility, progress) = Setup(start, hidden);
            progress.Unlock(start);

            Assert.IsFalse(visibility.IsVisible(hidden), "힌트 없이 만들어내야 하는 노드다.");

            progress.Unlock(hidden);
            Assert.IsTrue(visibility.IsVisible(hidden), "만들어내면 빈 공간에 나타난다.");
        }

        [Test]
        public void 파생_집중_모드에서는_감춰둔_하위가_보인다()
        {
            var start = Node("start", NodeType.Start);
            var derivative = Node("derivative", NodeType.Derivative, start);
            var inside = Node("inside", NodeType.Basic, derivative);

            var (visibility, progress) = Setup(start, derivative, inside);
            progress.Unlock(start);
            progress.Unlock(derivative);

            var focused = visibility.FocusedNodes(derivative).ToList();

            Assert.Contains(inside, focused, "집중 모드는 감춰둔 하위를 보려고 들어가는 것이다.");
            Assert.IsFalse(focused.Contains(start), "앞쪽 노드는 가려진다.");
        }

        [Test]
        public void 혼합_노드는_부모_중_하나만_해금돼도_도달_범위다()
        {
            var start = Node("start", NodeType.Start);
            var left = Node("left", NodeType.Basic, start);
            var right = Node("right", NodeType.Basic, start);
            var mixed = Node("mixed", NodeType.Mixed, left, right);

            var (visibility, progress) = Setup(start, left, right, mixed);
            progress.Unlock(start);
            progress.Unlock(left);

            Assert.IsTrue(visibility.IsWithinReach(mixed));
        }

        [Test]
        public void 해금되지_않은_파생_노드로는_집중_모드에_들어갈_수_없다()
        {
            var start = Node("start", NodeType.Start);
            var derivative = Node("derivative", NodeType.Derivative, start);
            var inside = Node("inside", NodeType.Basic, derivative);

            var (visibility, progress) = Setup(start, derivative, inside);
            progress.Unlock(start);

            Assert.IsFalse(visibility.CanFocus(derivative));

            progress.Unlock(derivative);
            Assert.IsTrue(visibility.CanFocus(derivative));
        }
    }
}
