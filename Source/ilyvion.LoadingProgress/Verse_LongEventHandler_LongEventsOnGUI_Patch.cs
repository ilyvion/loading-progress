using System.Reflection.Emit;
using ilyvion.LoadingProgress.FasterGameLoading;

namespace ilyvion.LoadingProgress;

// Shared by both patches below so the FasterGameLoading window's actual drawn side and the
// status box's layout math can't drift apart from each other.
internal static class FasterGameLoadingWindowLayout
{
    public static bool GoesAboveMainWindow(
        Vector2 mainWindowPosition,
        Vector2 mainWindowSize,
        Vector2 fasterGameLoadingWindowSize
    )
    {
        if (fasterGameLoadingWindowSize.y <= 0f)
        {
            return false;
        }

        var mainWindowBottom = mainWindowPosition.y + mainWindowSize.y;
        var fitsBelow = mainWindowBottom + 10f + fasterGameLoadingWindowSize.y <= UI.screenHeight;

        return !fitsBelow && mainWindowPosition.y - 10f - fasterGameLoadingWindowSize.y >= 0f;
    }
}

// Vanilla's own LongEventsOnGUI positions GameplayTipWindow/ModSummaryWindow itself, stacking
// them below a status box that it vertically centers on screen as one block, using local
// variables computed before our AdjustStatusWindowRect transpile point runs — moving the status
// box there doesn't move them, so the space vanilla reserved for it at the top of that block ends
// up empty on screen; GetAvoidanceExtent trims that off so it isn't treated as occupied.
// LoadingWindowPlacement.Middle/MiddleInverted also center our own window on screen; since
// vanilla centers the tip/mod-summary block independently of that, the two would either land on
// top of each other or, even clear of each other, read as one lopsided group instead of a single
// centered one. AdjustReservedBlockCenter (see the transpiler below) patches vanilla's own
// centering of that block instead of leaving it fixed, so that, together with our own window, the
// combined group is what's centered on screen. The two placements put the reserved block and our
// window (with its status box) on opposite sides of each other: MiddleInverted keeps the reserved
// block on top and places our window directly below it (ComputeBalancedReservedExtent /
// ComputeMiddleY), with the status box wherever ComputeStatusBoxTop finds room around our window;
// Middle instead puts our window (status box above it, via ComputeMiddleYWithStatusBoxAbove) on
// top and moves the reserved block below it instead (ComputeReservedExtentBelowOurWindow), so the
// on-screen order is status box, our window, then vanilla's own tip/mod-summary block.
// LoadingWindowPlacement.Custom lets the window go anywhere the user drags it, including on top
// of that same (unmoved) block, so it uses AvoidReservedExtent below to nudge clear of it instead;
// the status box that follows it around uses ComputeStatusBoxTop to avoid landing on that block
// too.
internal static class ExtraLongEventUIWindowLayout
{
    // Pure: centers a `width`x`height` block on `screenSize`, mirroring how vanilla centers the
    // status box/tip window/mod summary window block as one unit.
    internal static Rect ComputeReservedExtent(float width, float height, Vector2 screenSize) =>
        new((screenSize.x - width) / 2f, (screenSize.y - height) / 2f, width, height);

    public static Rect? GetReservedExtent()
    {
        var currentEvent = LongEventHandler.currentEvent;
        if (currentEvent == null)
        {
            return null;
        }

        var showTip =
            Find.UIRoot != null && !currentEvent.UseStandardWindow && currentEvent.showExtraUIInfo;
        if (!showTip)
        {
            return null;
        }

        var totalHeight = LongEventHandler.StatusRectSize.y + 17f + GameplayTipWindow.WindowSize.y;
        var width = GameplayTipWindow.WindowSize.x;
        if (Current.Game != null)
        {
            var modSummarySize = ModSummaryWindow.GetEffectiveSize();
            totalHeight += 17f + modSummarySize.y;
            width = Math.Max(width, modSummarySize.x);
        }

        return ComputeReservedExtent(
            width,
            totalHeight,
            new Vector2(UI.screenWidth, UI.screenHeight)
        );
    }

    // Pure: vanilla's own status box would normally occupy the top `statusBoxHeight + 17f` of
    // `reservedExtent`, but our AdjustStatusWindowRect patch always relocates it elsewhere, so
    // that strip is empty on screen; trims it off so avoidance logic doesn't treat it as occupied.
    internal static Rect TrimStatusBoxSpace(Rect reservedExtent, float statusBoxHeight)
    {
        var padding = statusBoxHeight + 17f;
        return new Rect(
            reservedExtent.x,
            reservedExtent.y + padding,
            reservedExtent.width,
            Math.Max(reservedExtent.height - padding, 0f)
        );
    }

    // The extent our own window/status box need to avoid: GetReservedExtent's block, minus the
    // top strip reserved for vanilla's own status box, which we never actually draw there.
    public static Rect? GetAvoidanceExtent() =>
        GetReservedExtent() is Rect reserved
            ? TrimStatusBoxSpace(reserved, LongEventHandler.StatusRectSize.y)
            : null;

    // Pure: where `avoidanceExtent` (vanilla's trimmed tip/mod-summary block) needs to sit so
    // that, stacked with a `ourCombinedHeight`-tall window directly below it (10px gap), the
    // combined group is centered on `screenHeight` as a whole, instead of `avoidanceExtent`
    // staying wherever vanilla's own (unrelated) centering originally put it.
    internal static Rect ComputeBalancedReservedExtent(
        Rect avoidanceExtent,
        float ourCombinedHeight,
        float screenHeight
    )
    {
        var groupTop = (screenHeight - avoidanceExtent.height - 10f - ourCombinedHeight) / 2f;
        return new Rect(avoidanceExtent.x, groupTop, avoidanceExtent.width, avoidanceExtent.height);
    }

    public static Rect? GetBalancedReservedExtent(float ourCombinedHeight) =>
        GetAvoidanceExtent() is Rect avoidanceExtent
            ? ComputeBalancedReservedExtent(avoidanceExtent, ourCombinedHeight, UI.screenHeight)
            : null;

    public static float ComputeMiddleY(Vector2 windowSize, Vector2 fasterGameLoadingWindowSize) =>
        ComputeMiddleY(
            windowSize,
            fasterGameLoadingWindowSize,
            GetAvoidanceExtent(),
            UI.screenHeight
        );

    // Pure core of ComputeMiddleY above, taking the reserved extent and screen height as
    // parameters instead of reading them live, so it can be exercised in tests.
    internal static float ComputeMiddleY(
        Vector2 windowSize,
        Vector2 fasterGameLoadingWindowSize,
        Rect? reservedExtent,
        float screenHeight
    )
    {
        var combinedHeight = windowSize.y + fasterGameLoadingWindowSize.y;
        if (reservedExtent is not Rect reserved)
        {
            return (screenHeight - combinedHeight) / 2f;
        }

        // Placed directly below wherever AdjustReservedBlockCenter (via
        // ComputeBalancedReservedExtent) puts the reserved extent, so the two form one centered
        // group instead of our own window being centered independently of it.
        var balanced = ComputeBalancedReservedExtent(reserved, combinedHeight, screenHeight);
        return balanced.yMax + 10f;
    }

    public static float ComputeMiddleYWithStatusBoxAbove(
        Vector2 windowSize,
        Vector2 fasterGameLoadingWindowSize
    ) =>
        ComputeMiddleYWithStatusBoxAbove(
            windowSize,
            fasterGameLoadingWindowSize,
            LongEventHandler.StatusRectSize.y,
            GetAvoidanceExtent(),
            UI.screenHeight
        );

    // Pure core of ComputeMiddleYWithStatusBoxAbove above, for LoadingWindowPlacement.Middle:
    // unlike ComputeMiddleY/MiddleInverted, where our own window (and status box) sit below the
    // reserved extent, here the whole reserved extent moves below our own window instead (see
    // ComputeReservedExtentBelowOurWindow), so our window sits at the very top of the centered
    // group, `statusBoxHeight + 10f` below that top to leave room for the status box above it.
    internal static float ComputeMiddleYWithStatusBoxAbove(
        Vector2 windowSize,
        Vector2 fasterGameLoadingWindowSize,
        float statusBoxHeight,
        Rect? reservedExtent,
        float screenHeight
    )
    {
        var leadingHeight = statusBoxHeight + 10f;
        var ourBlockHeight = leadingHeight + windowSize.y + fasterGameLoadingWindowSize.y;
        if (reservedExtent is not Rect reserved)
        {
            return ((screenHeight - ourBlockHeight) / 2f) + leadingHeight;
        }

        // ComputeBalancedReservedExtent's own `.y` is the top of the whole centered group
        // regardless of which element it's computing a position for; here that's our own
        // block's top rather than the reserved extent's, since the reserved extent now goes
        // below us instead of above.
        var groupTop = ComputeBalancedReservedExtent(reserved, ourBlockHeight, screenHeight).y;
        return groupTop + leadingHeight;
    }

    // Pure: where `avoidanceExtent` needs to sit so that, with a `ourBlockHeight`-tall window
    // directly above it (10px gap), the combined group is centered on `screenHeight` as a whole -
    // the mirror image of ComputeBalancedReservedExtent, for LoadingWindowPlacement.Middle, where
    // our own window (and its status box) come first and the reserved extent follows below them.
    internal static Rect ComputeReservedExtentBelowOurWindow(
        Rect avoidanceExtent,
        float ourBlockHeight,
        float screenHeight
    )
    {
        var groupTop = ComputeBalancedReservedExtent(
            avoidanceExtent,
            ourBlockHeight,
            screenHeight
        ).y;
        return new Rect(
            avoidanceExtent.x,
            groupTop + ourBlockHeight + 10f,
            avoidanceExtent.width,
            avoidanceExtent.height
        );
    }

    public static Rect? GetReservedExtentBelowOurWindow(float ourBlockHeight) =>
        GetAvoidanceExtent() is Rect avoidanceExtent
            ? ComputeReservedExtentBelowOurWindow(avoidanceExtent, ourBlockHeight, UI.screenHeight)
            : null;

    public static Vector2 AvoidReservedExtent(Vector2 position, Vector2 windowSize) =>
        AvoidReservedExtent(position, windowSize, GetAvoidanceExtent(), UI.screenHeight);

    // Pure core of AvoidReservedExtent above: nudges `position` so a `windowSize` window there
    // clears `reservedExtent`, preferring whichever side (above/below) is closer to `position`
    // and actually fits within `screenHeight`, taking parameters instead of reading them live so
    // it can be exercised in tests.
    internal static Vector2 AvoidReservedExtent(
        Vector2 position,
        Vector2 windowSize,
        Rect? reservedExtent,
        float screenHeight
    )
    {
        if (reservedExtent is not Rect reserved)
        {
            return position;
        }

        var windowRect = new Rect(position.x, position.y, windowSize.x, windowSize.y);
        if (!windowRect.Overlaps(reserved))
        {
            return position;
        }

        var above = reserved.y - 10f - windowSize.y;
        var below = reserved.yMax + 10f;
        var fitsAbove = above >= 0f;
        var fitsBelow = below + windowSize.y <= screenHeight;

        var preferAbove =
            fitsAbove
            && (!fitsBelow || Math.Abs(above - position.y) <= Math.Abs(below - position.y));
        var newY =
            preferAbove ? above
            : fitsBelow ? below
            : above;

        return new Vector2(
            position.x,
            Math.Clamp(newY, 0f, Math.Max(screenHeight - windowSize.y, 0f))
        );
    }

    // Pure: the top/bottom of the main window plus its FasterGameLoading window, treated as one
    // combined block, used to work out where else (the status box) can still fit without
    // overlapping either of them.
    internal static (float Top, float Bottom) ComputeBlockExtent(
        float mainWindowY,
        Vector2 mainWindowSize,
        Vector2 fasterGameLoadingWindowSize,
        bool fasterGameLoadingGoesAbove
    )
    {
        var top = fasterGameLoadingGoesAbove
            ? mainWindowY - 10f - fasterGameLoadingWindowSize.y
            : mainWindowY;
        var bottom = fasterGameLoadingGoesAbove
            ? mainWindowY + mainWindowSize.y
            : mainWindowY
                + mainWindowSize.y
                + (fasterGameLoadingWindowSize.y > 0 ? 10f + fasterGameLoadingWindowSize.y : 0f);
        return (top, bottom);
    }

    // Pure: picks where the status box goes relative to the main window/FasterGameLoading block
    // (`blockTop`/`blockBottom`), preferring above unless that spot would run off-screen or land
    // on the reserved extent, in which case it goes below instead.
    internal static float ComputeStatusBoxTop(
        float blockTop,
        float blockBottom,
        Rect aboveCandidate,
        Rect? reservedExtent
    )
    {
        var fitsAbove =
            blockTop >= aboveCandidate.height + 20f
            && (reservedExtent is not Rect reserved || !aboveCandidate.Overlaps(reserved));
        return fitsAbove ? aboveCandidate.y : blockBottom + 10f;
    }
}

// Invoked from two places, depending on which of LongEventsOnGUI's two branches vanilla takes for
// the current event (Verse_LongEventHandler_LongEventsOnGUI_Patch's transpiler / this file's
// WindowStack.WindowStackOnGUI Prefix below):
// - The raw-content branch (no game/world, or UseStandardWindow false): LongEventsOnGUI draws its
//   own full-screen background, then its own content, then calls TooltipHandler.DoTooltipGUI()
//   directly, all within its own body - so this is spliced in via a transpiler right after that
//   content call, landing after the background paint but before the tooltip call. A Prefix on the
//   whole method would run before the background paint and get covered by it; a Postfix would run
//   after the tooltip call and always composite above it.
// - The DrawLongEventWindow branch (UseStandardWindow true, e.g. during map generation with the
//   world view visible): in this case Root.OnGUI goes on to call uiRoot.UIRootOnGUI() in the same
//   frame, which draws the world (including its object icons) and then the real window stack,
//   after LongEventsOnGUI has already returned. Splicing in right after DrawLongEventWindow, like
//   the other branch, would draw before the world and get painted over by it; a Prefix on
//   WindowStack.WindowStackOnGUI instead lands after the world (and after the tooltip, drawn even
//   earlier) but before any real window, matching where a normal window would draw.
internal static class Verse_LongEventHandler_DrawOwnWindow_Patch
{
    internal static bool ShouldDrawOwnWindow(out bool useInGameWindow)
    {
        var currentEvent = LongEventHandler.currentEvent;
        if (currentEvent == null || currentEvent.forceHideUI)
        {
            useInGameWindow = false;
            return false;
        }

        useInGameWindow =
            LoadingProgressWindow.CurrentStage == LoadingStage.Finished
            && InGameLoadingSession.IsActive;
        return LoadingProgressWindow.CurrentStage != LoadingStage.Finished || useInGameWindow;
    }

    internal static void Draw()
    {
        if (!ShouldDrawOwnWindow(out var useInGameWindow))
        {
            return;
        }

        var loadingProgressWindowSize = useInGameWindow
            ? InGameLoadingWindow.WindowSize
            : LoadingProgressWindow.WindowSize;
        var fasterGameLoadingProgressWindowSize = FasterGameLoadingProgressWindow.WindowSize;
        var loadingProgressWindowCenteredX = (UI.screenWidth - loadingProgressWindowSize.x) / 2f;
        var loadingProgressWindowPosition = LoadingProgressMod
            .Settings
            .LoadingWindowPlacement switch
        {
            LoadingWindowPlacement.Top => new(
                loadingProgressWindowCenteredX,
                10f + LongEventHandler.StatusRectSize.y + 10f
            ),
            LoadingWindowPlacement.Middle => new(
                loadingProgressWindowCenteredX,
                ExtraLongEventUIWindowLayout.ComputeMiddleYWithStatusBoxAbove(
                    loadingProgressWindowSize,
                    fasterGameLoadingProgressWindowSize
                )
            ),
            LoadingWindowPlacement.MiddleInverted => new(
                loadingProgressWindowCenteredX,
                ExtraLongEventUIWindowLayout.ComputeMiddleY(
                    loadingProgressWindowSize,
                    fasterGameLoadingProgressWindowSize
                )
            ),
            LoadingWindowPlacement.Bottom => new(
                loadingProgressWindowCenteredX,
                UI.screenHeight
                    - loadingProgressWindowSize.y
                    - fasterGameLoadingProgressWindowSize.y
                    - 10f
                    - (fasterGameLoadingProgressWindowSize.y > 0 ? 10f : 0f)
            ),
            LoadingWindowPlacement.Custom => ExtraLongEventUIWindowLayout.AvoidReservedExtent(
                CustomPlacement.GetPosition(
                    loadingProgressWindowSize,
                    new Vector2(UI.screenWidth, UI.screenHeight),
                    LoadingProgressMod.Settings.CustomPlacementRelativePosition
                ),
                loadingProgressWindowSize
            ),
            _ => Vector2.zero,
        };

        Rect rect = new(
            loadingProgressWindowPosition.x,
            loadingProgressWindowPosition.y,
            loadingProgressWindowSize.x,
            loadingProgressWindowSize.y
        );

        Widgets.DrawShadowAround(rect);
        Widgets.DrawWindowBackground(rect);
        if (useInGameWindow)
        {
            InGameLoadingWindow.DrawContents(rect);
        }
        else
        {
            LoadingProgressWindow.DrawContents(rect);
        }

        var fasterGameLoadingGoesAbove = FasterGameLoadingWindowLayout.GoesAboveMainWindow(
            rect.position,
            rect.size,
            fasterGameLoadingProgressWindowSize
        );
        Vector2 fasterGameLoadingProgressWindowPosition = new(
            rect.x + ((rect.width - fasterGameLoadingProgressWindowSize.x) / 2f),
            fasterGameLoadingGoesAbove
                ? rect.y - 10f - fasterGameLoadingProgressWindowSize.y
                : rect.yMax + 10f
        );
        rect = new(
            fasterGameLoadingProgressWindowPosition.x,
            fasterGameLoadingProgressWindowPosition.y,
            fasterGameLoadingProgressWindowSize.x,
            fasterGameLoadingProgressWindowSize.y
        );
        Widgets.DrawShadowAround(rect);
        Widgets.DrawWindowBackground(rect);
        FasterGameLoadingProgressWindow.DrawContents(rect);
    }
}

// Draws our window alongside vanilla's own status box for LongEventsOnGUI's DrawLongEventWindow
// branch - see Verse_LongEventHandler_DrawOwnWindow_Patch's comment for why this can't be spliced
// into LongEventsOnGUI itself like the other branch. WindowStackOnGUI runs every frame regardless
// of any long event; Draw() itself is a no-op whenever ShouldDrawOwnWindow is false.
[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
internal static class Verse_WindowStack_WindowStackOnGUI_Patch
{
    internal static void Prefix() => Verse_LongEventHandler_DrawOwnWindow_Patch.Draw();
}

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsOnGUI))]
internal sealed class Verse_LongEventHandler_LongEventsOnGUI_Patch
{
    private static readonly MethodInfo _method_GenUI_Rounded = AccessTools.Method(
        typeof(GenUI),
        nameof(GenUI.Rounded),
        [typeof(Rect)]
    );
    private static readonly MethodInfo _methodAdjustStatusWindowRect = AccessTools.Method(
        typeof(Verse_LongEventHandler_LongEventsOnGUI_Patch),
        nameof(AdjustStatusWindowRect)
    );
    private static readonly FieldInfo _field_UI_screenHeight = AccessTools.Field(
        typeof(UI),
        nameof(UI.screenHeight)
    );
    private static readonly MethodInfo _methodAdjustReservedBlockCenter = AccessTools.Method(
        typeof(Verse_LongEventHandler_LongEventsOnGUI_Patch),
        nameof(AdjustReservedBlockCenter)
    );
    private static readonly MethodInfo _methodDrawLongEventWindowContents = AccessTools.Method(
        typeof(LongEventHandler),
        "DrawLongEventWindowContents"
    );
    private static readonly MethodInfo _methodDrawOwnWindow = AccessTools.Method(
        typeof(Verse_LongEventHandler_DrawOwnWindow_Patch),
        nameof(Verse_LongEventHandler_DrawOwnWindow_Patch.Draw)
    );

    // Vanilla computes this (its local num3) as half of screenHeight minus the full
    // tip/mod-summary block height, to center that block on screen; the transpiler below
    // replaces it with ComputeBalancedReservedExtent's chosen top instead, for
    // LoadingWindowPlacement.Middle/MiddleInverted, so the block moves to make room for our own
    // window (and, for Middle, the status box stacked above it) instead of staying wherever
    // vanilla's own centering put it.
    private static float AdjustReservedBlockCenter(float num3)
    {
        var placement = LoadingProgressMod.Settings.LoadingWindowPlacement;
        if (
            placement
            is not (LoadingWindowPlacement.Middle or LoadingWindowPlacement.MiddleInverted)
        )
        {
            return num3;
        }

        var useInGameWindow =
            LoadingProgressWindow.CurrentStage == LoadingStage.Finished
            && InGameLoadingSession.IsActive;
        if (LoadingProgressWindow.CurrentStage == LoadingStage.Finished && !useInGameWindow)
        {
            return num3;
        }

        var loadingProgressWindowSize = useInGameWindow
            ? InGameLoadingWindow.WindowSize
            : LoadingProgressWindow.WindowSize;
        var fasterGameLoadingWindowSize = FasterGameLoadingProgressWindow.WindowSize;

        if (placement == LoadingWindowPlacement.Middle)
        {
            // Middle puts our own window (and its status box) at the top of the centered group
            // and moves the reserved extent below them instead, so it takes the mirror-image
            // helper rather than GetBalancedReservedExtent below.
            var ourBlockHeight =
                LongEventHandler.StatusRectSize.y
                + 10f
                + loadingProgressWindowSize.y
                + fasterGameLoadingWindowSize.y;
            return
                ExtraLongEventUIWindowLayout.GetReservedExtentBelowOurWindow(ourBlockHeight)
                    is Rect relocated
                ? relocated.y - (LongEventHandler.StatusRectSize.y + 17f)
                : num3;
        }

        var combinedHeight = loadingProgressWindowSize.y + fasterGameLoadingWindowSize.y;
        return
            ExtraLongEventUIWindowLayout.GetBalancedReservedExtent(combinedHeight) is Rect balanced
            ? balanced.y - (LongEventHandler.StatusRectSize.y + 17f)
            : num3;
    }

    private static Rect AdjustStatusWindowRect(Rect r)
    {
        var useInGameWindow =
            LoadingProgressWindow.CurrentStage == LoadingStage.Finished
            && InGameLoadingSession.IsActive;
        if (LoadingProgressWindow.CurrentStage == LoadingStage.Finished && !useInGameWindow)
        {
            return r;
        }

        var statusRectSize = LongEventHandler.StatusRectSize;
        var loadingProgressWindowSize = useInGameWindow
            ? InGameLoadingWindow.WindowSize
            : LoadingProgressWindow.WindowSize;
        var fasterGameLoadingProgressWindowSize = FasterGameLoadingProgressWindow.WindowSize;

        float statusRectTop = 0;
        switch (LoadingProgressMod.Settings.LoadingWindowPlacement)
        {
            case LoadingWindowPlacement.Top:
                statusRectTop = 10f;
                break;
            case LoadingWindowPlacement.Middle:
                // Middle always reserves `statusRectSize.y + 10f` directly above our window (see
                // ComputeMiddleYWithStatusBoxAbove) for the status box, so it goes right there
                // regardless of the reserved extent or the FasterGameLoading window.
                statusRectTop =
                    ExtraLongEventUIWindowLayout.ComputeMiddleYWithStatusBoxAbove(
                        loadingProgressWindowSize,
                        fasterGameLoadingProgressWindowSize
                    )
                    - 10f
                    - statusRectSize.y;
                break;
            case LoadingWindowPlacement.MiddleInverted:
                var middleCombinedHeight =
                    loadingProgressWindowSize.y + fasterGameLoadingProgressWindowSize.y;
                var middleAvoidanceExtent = ExtraLongEventUIWindowLayout.GetBalancedReservedExtent(
                    middleCombinedHeight
                );
                var middleY = ExtraLongEventUIWindowLayout.ComputeMiddleY(
                    loadingProgressWindowSize,
                    fasterGameLoadingProgressWindowSize
                );
                // MiddleInverted places our window flush against the (also relocated) reserved
                // extent, leaving no room for the status box on its usual side directly above the
                // main window; put it below instead.
                var middleFasterGoesAbove = FasterGameLoadingWindowLayout.GoesAboveMainWindow(
                    new Vector2(0f, middleY),
                    loadingProgressWindowSize,
                    fasterGameLoadingProgressWindowSize
                );
                var (middleBlockTop, middleBlockBottom) =
                    ExtraLongEventUIWindowLayout.ComputeBlockExtent(
                        middleY,
                        loadingProgressWindowSize,
                        fasterGameLoadingProgressWindowSize,
                        middleFasterGoesAbove
                    );
                var middleAboveCandidate = new Rect(
                    r.x,
                    middleBlockTop - statusRectSize.y - 10f,
                    r.width,
                    statusRectSize.y
                );
                statusRectTop = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
                    middleBlockTop,
                    middleBlockBottom,
                    middleAboveCandidate,
                    middleAvoidanceExtent
                );
                break;
            case LoadingWindowPlacement.Bottom:
                statusRectTop =
                    UI.screenHeight
                    - loadingProgressWindowSize.y
                    - fasterGameLoadingProgressWindowSize.y
                    - 10f
                    - (fasterGameLoadingProgressWindowSize.y > 0 ? 10f : 0f)
                    - statusRectSize.y
                    - 10f;
                break;
            case LoadingWindowPlacement.Custom:
                var avoidanceExtent = ExtraLongEventUIWindowLayout.GetAvoidanceExtent();
                var customPosition = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
                    CustomPlacement.GetPosition(
                        loadingProgressWindowSize,
                        new Vector2(UI.screenWidth, UI.screenHeight),
                        LoadingProgressMod.Settings.CustomPlacementRelativePosition
                    ),
                    loadingProgressWindowSize,
                    avoidanceExtent,
                    UI.screenHeight
                );
                // The loading window can be dragged anywhere, including flush against the top or
                // bottom of the screen, so there isn't always room to put the status box on its
                // usual side, or for the FasterGameLoading window to stay below it; work out where
                // the FasterGameLoading window actually ends up (same logic used to draw it) and
                // put the status box on whichever side of the combined block still has room.
                var fasterGameLoadingGoesAbove = FasterGameLoadingWindowLayout.GoesAboveMainWindow(
                    customPosition,
                    loadingProgressWindowSize,
                    fasterGameLoadingProgressWindowSize
                );
                var (blockTop, blockBottom) = ExtraLongEventUIWindowLayout.ComputeBlockExtent(
                    customPosition.y,
                    loadingProgressWindowSize,
                    fasterGameLoadingProgressWindowSize,
                    fasterGameLoadingGoesAbove
                );
                // r.width is the box's actual current width, which can be wider than
                // LongEventHandler.StatusRectSize.x when the status text is long; centering on
                // the static field's width instead of the real one is what threw this off.
                r.x = Math.Clamp(
                    customPosition.x + ((loadingProgressWindowSize.x - r.width) / 2f),
                    0f,
                    UI.screenWidth - r.width
                );
                var aboveCandidate = new Rect(
                    r.x,
                    blockTop - statusRectSize.y - 10f,
                    r.width,
                    statusRectSize.y
                );
                statusRectTop = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
                    blockTop,
                    blockBottom,
                    aboveCandidate,
                    avoidanceExtent
                );
                break;
            default:
                break;
        }
        r.y = Math.Clamp(statusRectTop, 0f, UI.screenHeight - statusRectSize.y);
        return r;
    }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        var originalInstructionList = instructions.ToList();

        var codeMatcher = new CodeMatcher(originalInstructionList, generator);

        // Vanilla's local `num3` (half of screenHeight minus the tip/mod-summary block's full
        // height) is computed as `(float)UI.screenHeight - <block height> / 2f` and immediately
        // stored; the field read of UI.screenHeight only appears here, so it's a reliable anchor
        // to land right before that store and replace the value about to be stored.
        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Ldsfld && i.operand is FieldInfo f && f == _field_UI_screenHeight
        );
        if (!codeMatcher.IsValid)
        {
            LoadingProgressMod.Error(
                $"Could not patch LongEventHandler.LongEventsOnGUI, IL does not match expectations ([ldsfld UI.screenHeight])"
            );
            return originalInstructionList;
        }

        _ = codeMatcher.Advance(6).Insert([new(OpCodes.Call, _methodAdjustReservedBlockCenter)]);

        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _method_GenUI_Rounded
        );
        if (!codeMatcher.IsValid)
        {
            LoadingProgressMod.Error(
                $"Could not patch LongEventHandler.LongEventsOnGUI, IL does not match expectations ([call GenUI.Rounded])"
            );
            return originalInstructionList;
        }

        _ = codeMatcher.Advance(1).Insert([new(OpCodes.Call, _methodAdjustStatusWindowRect)]);

        // Splices our own window's draw call in directly after vanilla's own content call, landing
        // after vanilla's full-screen background paint (drawn just before it) but before its
        // internal TooltipHandler.DoTooltipGUI() call - see Verse_LongEventHandler_DrawOwnWindow_
        // Patch's comment for why neither a Prefix nor a Postfix on the whole method can land in
        // that same spot. Only covers the raw-content branch (DrawLongEventWindowContents); the
        // other branch (DrawLongEventWindow) is handled by the WindowStack.WindowStackOnGUI Prefix
        // below instead, for the same reason.
        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Call
            && i.operand is MethodInfo m
            && m == _methodDrawLongEventWindowContents
        );
        if (!codeMatcher.IsValid)
        {
            LoadingProgressMod.Error(
                $"Could not patch LongEventHandler.LongEventsOnGUI, IL does not match expectations ([call DrawLongEventWindowContents])"
            );
            return originalInstructionList;
        }

        _ = codeMatcher.Advance(1).Insert([new(OpCodes.Call, _methodDrawOwnWindow)]);

        return codeMatcher.Instructions();
    }
}
