using System.Linq;
using NUnit.Framework;
using PoorSmith.Data;
using UnityEngine;
using static PoorSmith.Crafting.Tests.NodeBuilder;

namespace PoorSmith.Crafting.Tests
{
    public sealed class NodeLayoutTests
    {
        static readonly Vector2 Spacing = new(200f, 100f);

        [Test]
        public void 뿌리는_왼쪽에_자식은_오른쪽에_놓인다()
        {
            var start = Node("start", NodeType.Start);
            var child = Node("child", NodeType.Basic, start);
            var grandchild = Node("grandchild", NodeType.Basic, child);
            var nodes = new[] { start, child, grandchild };

            var layout = new NodeLayout(new NodeGraph(nodes), nodes, Spacing);

            Assert.AreEqual(0f, layout.Positions[start].x);
            Assert.Less(layout.Positions[start].x, layout.Positions[child].x);
            Assert.Less(layout.Positions[child].x, layout.Positions[grandchild].x);
        }

        [Test]
        public void 같은_층의_노드는_서로_다른_높이에_놓인다()
        {
            var start = Node("start", NodeType.Start);
            var a = Node("a", NodeType.Basic, start);
            var b = Node("b", NodeType.Basic, start);
            var c = Node("c", NodeType.Basic, start);
            var nodes = new[] { start, a, b, c };

            var layout = new NodeLayout(new NodeGraph(nodes), nodes, Spacing);

            var ys = new[] { a, b, c }.Select(n => layout.Positions[n].y).ToList();
            CollectionAssert.AllItemsAreUnique(ys);
        }

        [Test]
        public void 혼합_노드는_더_먼_부모_뒤에_놓인다()
        {
            // left는 1층, right는 2층. 혼합 노드는 3층에 놓여야 선이 뒤로 가지 않는다.
            var start = Node("start", NodeType.Start);
            var left = Node("left", NodeType.Basic, start);
            var middle = Node("middle", NodeType.Basic, start);
            var right = Node("right", NodeType.Basic, middle);
            var mixed = Node("mixed", NodeType.Mixed, left, right);
            var nodes = new[] { start, left, middle, right, mixed };

            var layout = new NodeLayout(new NodeGraph(nodes), nodes, Spacing);

            Assert.Greater(layout.Positions[mixed].x, layout.Positions[right].x);
        }

        [Test]
        public void 좌표가_지정된_노드는_그_자리를_지킨다()
        {
            var start = Node("start", NodeType.Start);
            var pinned = Node("pinned", NodeType.Basic, start);
            var so = new UnityEditor.SerializedObject(pinned);
            so.FindProperty("useManualPosition").boolValue = true;
            so.FindProperty("mapPosition").vector2Value = new Vector2(999f, -42f);
            so.ApplyModifiedPropertiesWithoutUndo();

            var nodes = new[] { start, pinned };
            var layout = new NodeLayout(new NodeGraph(nodes), nodes, Spacing);

            Assert.AreEqual(new Vector2(999f, -42f), layout.Positions[pinned]);
        }

        [Test]
        public void 빈_목록이어도_터지지_않는다()
        {
            var layout = new NodeLayout(new NodeGraph(new NodeDef[0]), new NodeDef[0], Spacing);

            Assert.IsEmpty(layout.Positions);
            Assert.AreEqual(Vector2.zero, layout.Size);
        }
    }
}
