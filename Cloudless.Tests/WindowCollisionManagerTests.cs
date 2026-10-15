using System.Windows;
using Xunit;

namespace Cloudless.Tests;

public sealed class WindowCollisionManagerTests
{
    private static readonly Rect DisplayBounds = new(0, 0, 200, 200);

    [Fact]
    public void ConstrainBounds_StopsAtPeerAndSlidesAlongItsEdge()
    {
        Rect current = new(10, 10, 20, 20);
        Rect peer = new(40, 0, 20, 50);
        Rect proposed = new(60, 30, 20, 20);

        Rect result = WindowCollisionManager.ConstrainBounds(current, proposed, DisplayBounds, new[] { peer }, isResizing: false);

        Assert.Equal(new Rect(20, 30, 20, 20), result);
    }

    [Fact]
    public void ConstrainBounds_ClampsMovesToDisplayBounds()
    {
        Rect current = new(10, 10, 20, 20);
        Rect proposed = new(-10, 10, 20, 20);

        Rect result = WindowCollisionManager.ConstrainBounds(current, proposed, DisplayBounds, Array.Empty<Rect>(), isResizing: false);

        Assert.Equal(new Rect(0, 10, 20, 20), result);
    }

    [Fact]
    public void ConstrainBounds_BlocksMovementStartingAtTouchingEdges()
    {
        Rect current = new(10, 10, 20, 20);
        Rect peer = new(30, 0, 20, 50);
        Rect proposed = new(20, 10, 20, 20);

        Rect result = WindowCollisionManager.ConstrainBounds(current, proposed, DisplayBounds, new[] { peer }, isResizing: false);

        Assert.Equal(current, result);
    }

    [Fact]
    public void ConstrainBounds_ClampsResizeBeforeItOverlapsPeer()
    {
        Rect current = new(10, 10, 20, 20);
        Rect peer = new(40, 0, 20, 50);
        Rect proposed = new(10, 10, 50, 20);

        Rect result = WindowCollisionManager.ConstrainBounds(current, proposed, DisplayBounds, new[] { peer }, isResizing: true);

        Assert.Equal(new Rect(10, 10, 30, 20), result);
    }

    [Fact]
    public void ConstrainBounds_BlocksResizeStartingAtTouchingEdges()
    {
        Rect current = new(10, 10, 20, 20);
        Rect peer = new(30, 0, 20, 50);
        Rect proposed = new(10, 10, 30, 20);

        Rect result = WindowCollisionManager.ConstrainBounds(current, proposed, DisplayBounds, new[] { peer }, isResizing: true);

        Assert.Equal(current, result);
    }

    [Fact]
    public void ConstrainBounds_LeavesInitiallyOverlappingPeerUnconstrained()
    {
        Rect current = new(30, 10, 20, 20);
        Rect peer = new(40, 0, 20, 50);
        Rect proposed = new(60, 10, 20, 20);

        Rect result = WindowCollisionManager.ConstrainBounds(current, proposed, DisplayBounds, new[] { peer }, isResizing: false);

        Assert.Equal(proposed, result);
    }
}
