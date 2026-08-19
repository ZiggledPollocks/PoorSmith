using System.Linq;
using NUnit.Framework;
using PoorSmith.Data;
using static PoorSmith.Crafting.Tests.NodeBuilder;

namespace PoorSmith.Crafting.Tests
{
    /// <summary>노드 데이터가 잘못됐을 때 조용히 넘어가지 않고 알려주는지.</summary>
    public sealed class NodeGraphTests
    {
        [Test]
        public void 자식_목록은_부모_참조에서_만들어진다()
        {
            var start = Node("start", NodeType.Start);
            var a = Node("a", NodeType.Basic, start);
            var b = Node("b", NodeType.Basic, start);

            var graph = new NodeGraph(new[] { start, a, b });

            Assert.AreEqual(2, graph.ChildrenOf(start).Count);
            CollectionAssert.AreEquivalent(new[] { a, b }, graph.ChildrenOf(start));
            Assert.IsEmpty(graph.Problems);
        }

        [Test]
        public void 부모가_없는_비시작_노드는_문제로_잡힌다()
        {
            var orphan = Node("orphan");

            var graph = new NodeGraph(new[] { orphan });

            Assert.IsTrue(graph.Problems.Any(p => p.Contains("orphan")));
        }

        [Test]
        public void 혼합_노드의_부모가_둘이_아니면_문제로_잡힌다()
        {
            var start = Node("start", NodeType.Start);
            var mixed = Node("mixed", NodeType.Mixed, start);

            var graph = new NodeGraph(new[] { start, mixed });

            Assert.IsTrue(graph.Problems.Any(p => p.Contains("혼합")));
        }

        [Test]
        public void 종결_노드에_하위가_있으면_문제로_잡힌다()
        {
            var start = Node("start", NodeType.Start);
            var terminal = Node("terminal", NodeType.Terminal, start);
            var after = Node("after", NodeType.Basic, terminal);

            var graph = new NodeGraph(new[] { start, terminal, after });

            Assert.IsTrue(graph.Problems.Any(p => p.Contains("종결")));
        }

        [Test]
        public void 부모_관계가_순환하면_무한루프에_빠지지_않고_문제로_잡힌다()
        {
            // 서로를 부모로 지정한 상태를 만든다.
            var a = Node("a", NodeType.Start);
            var b = Node("b", NodeType.Basic, a);
            var so = new UnityEditor.SerializedObject(a);
            var parents = so.FindProperty("parents");
            parents.arraySize = 1;
            parents.GetArrayElementAtIndex(0).objectReferenceValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();

            var graph = new NodeGraph(new[] { a, b });

            Assert.IsTrue(graph.Problems.Any(p => p.Contains("순환")));
        }
    }
}
