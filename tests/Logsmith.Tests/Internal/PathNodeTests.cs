using System.Text;
using Logsmith.Internal;

namespace Logsmith.Tests.Internal;

[TestFixture]
public class PathNodeTests
{
    [Test]
    public void Segment_Get_ReturnsInitialValue()
    {
        var node = new PathNode("root");
        Assert.That(node.Segment, Is.EqualTo("root"));
    }

    [Test]
    public void Segment_Set_UpdatesValue()
    {
        var node = new PathNode("old");
        node.Segment = "new";
        Assert.That(node.Segment, Is.EqualTo("new"));
    }

    [Test]
    public void Segment_Set_IncrementsVersion()
    {
        var node = new PathNode("initial");
        var v1 = node.Version;
        node.Segment = "changed";
        Assert.That(node.Version, Is.GreaterThan(v1));
    }

    [Test]
    public void Parent_ReturnsParentNode()
    {
        var parent = new PathNode("parent");
        var child = new PathNode("child", parent);
        Assert.That(child.Parent, Is.SameAs(parent));
    }

    [Test]
    public void Parent_RootNode_ReturnsNull()
    {
        var root = new PathNode("root");
        Assert.That(root.Parent, Is.Null);
    }

    [Test]
    public void CalculateVersionSum_SingleNode()
    {
        var node = new PathNode("test");
        Assert.That(node.CalculateVersionSum(), Is.EqualTo(1));
    }

    [Test]
    public void CalculateVersionSum_Chain_SumsAll()
    {
        var root = new PathNode("root");
        var child = new PathNode("child", root);
        var grandchild = new PathNode("grandchild", child);

        // Each starts at version 1
        Assert.That(grandchild.CalculateVersionSum(), Is.EqualTo(3));
    }

    [Test]
    public void CalculateVersionSum_AfterMutation_Increases()
    {
        var root = new PathNode("root");
        var child = new PathNode("child", root);

        var before = child.CalculateVersionSum();
        root.Segment = "changed";
        var after = child.CalculateVersionSum();

        Assert.That(after, Is.GreaterThan(before));
    }

    [Test]
    public void WriteUtf8Path_SingleNode_WritesSegment()
    {
        var node = new PathNode("root");
        var buffer = new byte[64];
        var written = node.WriteUtf8Path(buffer);

        var result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.That(result, Is.EqualTo("root"));
    }

    [Test]
    public void WriteUtf8Path_Chain_WritesSeparated()
    {
        var root = new PathNode("root");
        var child = new PathNode("child", root);
        var grandchild = new PathNode("leaf", child);

        var buffer = new byte[64];
        var written = grandchild.WriteUtf8Path(buffer);

        var result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.That(result, Is.EqualTo("root|child|leaf"));
    }

    [Test]
    public void WriteUtf8Path_NullSegment_Skipped()
    {
        var root = new PathNode("root");
        var middle = new PathNode(null, root);
        var leaf = new PathNode("leaf", middle);

        var buffer = new byte[64];
        var written = leaf.WriteUtf8Path(buffer);

        var result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.That(result, Is.EqualTo("root|leaf"));
    }

    [Test]
    public void WriteUtf8Path_EmptySegment_Skipped()
    {
        var root = new PathNode("root");
        var middle = new PathNode("", root);
        var leaf = new PathNode("leaf", middle);

        var buffer = new byte[64];
        var written = leaf.WriteUtf8Path(buffer);

        var result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.That(result, Is.EqualTo("root|leaf"));
    }

    [Test]
    public void WriteUtf8Path_AllNullSegments_ReturnsZero()
    {
        var root = new PathNode(null);
        var child = new PathNode(null, root);

        var buffer = new byte[64];
        var written = child.WriteUtf8Path(buffer);

        Assert.That(written, Is.EqualTo(0));
    }

    [Test]
    public void WriteUtf8Path_UnicodeSegment_WritesUtf8()
    {
        var node = new PathNode("\u00e9v\u00e9nement"); // événement
        var buffer = new byte[64];
        var written = node.WriteUtf8Path(buffer);

        var result = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.That(result, Is.EqualTo("\u00e9v\u00e9nement"));
    }

    [Test]
    public void CalculateMaxByteCount_SingleNode()
    {
        var node = new PathNode("test");
        var maxBytes = node.CalculateMaxByteCount();
        Assert.That(maxBytes, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void CalculateMaxByteCount_Chain_IncludesSeparators()
    {
        var root = new PathNode("a");
        var child = new PathNode("b", root);

        var maxBytes = child.CalculateMaxByteCount();
        // "a|b" — at least 3 bytes but max byte count uses GetMaxByteCount which overestimates
        Assert.That(maxBytes, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void ConcurrentSegmentMutation_DoesNotCorrupt()
    {
        var node = new PathNode("initial");
        var iterations = 10_000;
        var barrier = new Barrier(2);

        var writer = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
                node.Segment = $"seg{i}";
        });

        var reader = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                var seg = node.Segment;
                // Should never be null after initial set (we never set null)
                Assert.That(seg, Is.Not.Null);
            }
        });

        Task.WaitAll(writer, reader);
    }

    [Test]
    public void ConcurrentPathReading_DoesNotThrow()
    {
        var root = new PathNode("root");
        var child = new PathNode("child", root);
        var iterations = 10_000;
        var barrier = new Barrier(2);

        var mutator = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
                root.Segment = $"root{i}";
        });

        var pathReader = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                var buffer = new byte[128];
                var written = child.WriteUtf8Path(buffer);
                Assert.That(written, Is.GreaterThan(0));
            }
        });

        Task.WaitAll(mutator, pathReader);
    }
}
