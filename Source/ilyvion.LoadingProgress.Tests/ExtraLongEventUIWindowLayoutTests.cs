using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ExtraLongEventUIWindowLayoutTests
{
    [Test]
    public static void ComputeReservedExtentCentersTheBlockOnScreen()
    {
        var rect = ExtraLongEventUIWindowLayout.ComputeReservedExtent(
            400f,
            200f,
            new Vector2(1920f, 1080f)
        );
        Expect.AreEqual(760f, rect.x);
        Expect.AreEqual(440f, rect.y);
        Expect.AreEqual(400f, rect.width);
        Expect.AreEqual(200f, rect.height);
    }

    [Test]
    public static void TrimStatusBoxSpaceRemovesTheTopStripReservedForVanillasStatusBox()
    {
        var reserved = new Rect(100f, 200f, 400f, 500f);
        var trimmed = ExtraLongEventUIWindowLayout.TrimStatusBoxSpace(
            reserved,
            statusBoxHeight: 70f
        );
        Expect.AreEqual(100f, trimmed.x);
        Expect.AreEqual(287f, trimmed.y);
        Expect.AreEqual(400f, trimmed.width);
        Expect.AreEqual(413f, trimmed.height);
    }

    [Test]
    public static void TrimStatusBoxSpaceNeverGoesNegativeWhenThePaddingExceedsTheHeight()
    {
        var reserved = new Rect(0f, 0f, 100f, 50f);
        var trimmed = ExtraLongEventUIWindowLayout.TrimStatusBoxSpace(
            reserved,
            statusBoxHeight: 70f
        );
        Expect.AreEqual(0f, trimmed.height);
    }

    [Test]
    public static void AvoidReservedExtentLeavesPositionAloneWhenThereIsNoReservedExtent()
    {
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(100f, 100f),
            new Vector2(200f, 100f),
            reservedExtent: null,
            screenHeight: 1080f
        );
        Expect.AreEqual(100f, position.x);
        Expect.AreEqual(100f, position.y);
    }

    [Test]
    public static void AvoidReservedExtentLeavesPositionAloneWhenItDoesNotOverlap()
    {
        // Custom placement can put the window anywhere, including corners far from vanilla's
        // horizontally-centered tip/mod-summary block; those placements shouldn't be nudged.
        var reserved = new Rect(700f, 400f, 500f, 300f);
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(0f, 0f),
            new Vector2(300f, 100f),
            reserved,
            screenHeight: 1080f
        );
        Expect.AreEqual(0f, position.x);
        Expect.AreEqual(0f, position.y);
    }

    [Test]
    public static void AvoidReservedExtentMovesBelowWhenThatSideIsCloserAndFits()
    {
        var reserved = new Rect(700f, 400f, 500f, 300f);
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(700f, 450f),
            new Vector2(500f, 300f),
            reserved,
            screenHeight: 1080f
        );
        Expect.AreEqual(700f, position.x);
        Expect.AreEqual(710f, position.y);
    }

    [Test]
    public static void AvoidReservedExtentMovesAboveWhenThatSideIsCloserAndFits()
    {
        var reserved = new Rect(700f, 400f, 500f, 300f);
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(700f, 350f),
            new Vector2(500f, 300f),
            reserved,
            screenHeight: 1080f
        );
        Expect.AreEqual(700f, position.x);
        Expect.AreEqual(90f, position.y);
    }

    [Test]
    public static void AvoidReservedExtentClampsOnScreenWhenNeitherSideFits()
    {
        var reserved = new Rect(700f, 400f, 500f, 300f);
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(700f, 450f),
            new Vector2(500f, 500f),
            reserved,
            screenHeight: 900f
        );
        Expect.AreEqual(700f, position.x);
        Expect.AreEqual(0f, position.y);
    }

    [Test]
    public static void ComputeMiddleYReturnsTheNaturalCenterWhenThereIsNoReservedExtent()
    {
        var y = ExtraLongEventUIWindowLayout.ComputeMiddleY(
            new Vector2(400f, 200f),
            new Vector2(0f, 0f),
            reservedExtent: null,
            screenHeight: 1000f
        );
        Expect.AreEqual(400f, y);
    }

    [Test]
    public static void ComputeMiddleYPlacesTheWindowJustBelowTheBalancedReservedExtent()
    {
        // The reserved extent's own (unbalanced) position is irrelevant here - only its height
        // matters, since ComputeBalancedReservedExtent decides where it actually goes.
        var reserved = new Rect(0f, 999f, 100f, 200f);
        var y = ExtraLongEventUIWindowLayout.ComputeMiddleY(
            new Vector2(400f, 300f),
            new Vector2(0f, 0f),
            reserved,
            screenHeight: 1200f
        );
        Expect.AreEqual(555f, y);
    }

    [Test]
    public static void ComputeMiddleYWithStatusBoxAboveReturnsTheNaturalCenterWhenThereIsNoReservedExtent()
    {
        // With no reserved extent, the group (status box + gap + our window) is centered as a
        // whole, so our window sits `statusBoxHeight + 10f` below that group's centered top.
        var y = ExtraLongEventUIWindowLayout.ComputeMiddleYWithStatusBoxAbove(
            new Vector2(400f, 200f),
            new Vector2(0f, 0f),
            statusBoxHeight: 40f,
            reservedExtent: null,
            screenHeight: 1000f
        );
        // combinedHeight = 40f + 10f + 200f = 250f; group top = (1000f - 250f) / 2f = 375f;
        // our window sits 50f (statusBoxHeight + 10f) below that.
        Expect.AreEqual(425f, y);
    }

    [Test]
    public static void ComputeMiddleYWithStatusBoxAbovePlacesTheWindowAtTheTopOfTheCenteredGroupWhenThereIsAReservedExtent()
    {
        // Unlike ComputeMiddleY, the reserved extent moves below our window here (see
        // ComputeReservedExtentBelowOurWindow), so only its height (not its own position) feeds
        // into where our window ends up.
        var reserved = new Rect(0f, 999f, 100f, 200f);
        var y = ExtraLongEventUIWindowLayout.ComputeMiddleYWithStatusBoxAbove(
            new Vector2(400f, 300f),
            new Vector2(0f, 0f),
            statusBoxHeight: 40f,
            reservedExtent: reserved,
            screenHeight: 1200f
        );
        // ourBlockHeight = 40f + 10f + 300f = 350f; groupTop = (1200f - 200f - 10f - 350f) / 2f =
        // 320f; our window sits 50f (statusBoxHeight + 10f) below that.
        Expect.AreEqual(370f, y);
    }

    [Test]
    public static void ComputeReservedExtentBelowOurWindowCentersTheCombinedGroupOnScreen()
    {
        var avoidanceExtent = new Rect(50f, 0f, 400f, 200f);
        var relocated = ExtraLongEventUIWindowLayout.ComputeReservedExtentBelowOurWindow(
            avoidanceExtent,
            ourBlockHeight: 300f,
            screenHeight: 1200f
        );
        Expect.AreEqual(50f, relocated.x);
        Expect.AreEqual(400f, relocated.width);
        Expect.AreEqual(200f, relocated.height);
        // groupTop = (1200f - 200f - 10f - 300f) / 2f = 345f; relocated sits 300f + 10f below
        // that.
        Expect.AreEqual(655f, relocated.y);
        // Top margin (above our window, which starts at groupTop) and bottom margin (below
        // relocated) both come out to 345px - a centered group.
        Expect.AreEqual(345f, 1200f - (relocated.y + 200f));
    }

    [Test]
    public static void ComputeBalancedReservedExtentCentersTheCombinedGroupOnScreen()
    {
        var avoidanceExtent = new Rect(50f, 0f, 400f, 200f);
        var balanced = ExtraLongEventUIWindowLayout.ComputeBalancedReservedExtent(
            avoidanceExtent,
            ourCombinedHeight: 300f,
            screenHeight: 1200f
        );
        Expect.AreEqual(50f, balanced.x);
        Expect.AreEqual(345f, balanced.y);
        Expect.AreEqual(400f, balanced.width);
        Expect.AreEqual(200f, balanced.height);
        // Top margin (above balanced.y) and bottom margin (below our own window, which sits
        // balanced.height + 10px below balanced.y) both come out to 345px - a centered group.
        Expect.AreEqual(balanced.y, 1200f - (balanced.y + 200f + 10f + 300f));
    }

    [Test]
    public static void ComputeBlockExtentSpansJustTheMainWindowWhenThereIsNoFasterGameLoadingWindow()
    {
        var (top, bottom) = ExtraLongEventUIWindowLayout.ComputeBlockExtent(
            mainWindowY: 100f,
            mainWindowSize: new Vector2(400f, 150f),
            fasterGameLoadingWindowSize: Vector2.zero,
            fasterGameLoadingGoesAbove: false
        );
        Expect.AreEqual(100f, top);
        Expect.AreEqual(250f, bottom);
    }

    [Test]
    public static void ComputeBlockExtentIncludesTheFasterGameLoadingWindowBelowWhenItGoesThere()
    {
        var (top, bottom) = ExtraLongEventUIWindowLayout.ComputeBlockExtent(
            mainWindowY: 100f,
            mainWindowSize: new Vector2(400f, 150f),
            fasterGameLoadingWindowSize: new Vector2(400f, 80f),
            fasterGameLoadingGoesAbove: false
        );
        Expect.AreEqual(100f, top);
        Expect.AreEqual(340f, bottom);
    }

    [Test]
    public static void ComputeBlockExtentIncludesTheFasterGameLoadingWindowAboveWhenItGoesThere()
    {
        var (top, bottom) = ExtraLongEventUIWindowLayout.ComputeBlockExtent(
            mainWindowY: 100f,
            mainWindowSize: new Vector2(400f, 150f),
            fasterGameLoadingWindowSize: new Vector2(400f, 80f),
            fasterGameLoadingGoesAbove: true
        );
        Expect.AreEqual(10f, top);
        Expect.AreEqual(250f, bottom);
    }

    [Test]
    public static void ComputeStatusBoxTopGoesAboveTheBlockWhenThereIsRoomAndNoReservedExtent()
    {
        var aboveCandidate = new Rect(100f, 190f, 200f, 50f);
        var top = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
            blockTop: 250f,
            blockBottom: 500f,
            aboveCandidate,
            reservedExtent: null
        );
        Expect.AreEqual(190f, top);
    }

    [Test]
    public static void ComputeStatusBoxTopGoesBelowTheBlockWhenThereIsNoRoomAbove()
    {
        var aboveCandidate = new Rect(100f, -30f, 200f, 50f);
        var top = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
            blockTop: 10f,
            blockBottom: 500f,
            aboveCandidate,
            reservedExtent: null
        );
        Expect.AreEqual(510f, top);
    }

    [Test]
    public static void ComputeStatusBoxTopGoesBelowTheBlockWhenAboveWouldOverlapTheReservedExtent()
    {
        // Enough vertical room above the block, but that space is where the gameplay tip/mod
        // summary block sits, so the status box needs to go below the main block instead.
        var aboveCandidate = new Rect(100f, 190f, 200f, 50f);
        var reserved = new Rect(0f, 150f, 400f, 300f);
        var top = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
            blockTop: 250f,
            blockBottom: 500f,
            aboveCandidate,
            reservedExtent: reserved
        );
        Expect.AreEqual(510f, top);
    }

    [Test]
    public static void ComputeStatusBoxTopGoesAboveWhenTheReservedExtentDoesNotOverlapTheCandidate()
    {
        var aboveCandidate = new Rect(100f, 190f, 200f, 50f);
        var reserved = new Rect(700f, 150f, 400f, 300f);
        var top = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
            blockTop: 250f,
            blockBottom: 500f,
            aboveCandidate,
            reservedExtent: reserved
        );
        Expect.AreEqual(190f, top);
    }
}
