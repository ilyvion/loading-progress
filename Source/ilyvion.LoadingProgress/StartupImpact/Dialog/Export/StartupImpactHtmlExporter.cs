using System.Text;

namespace ilyvion.LoadingProgress.StartupImpact.Dialog.Export;

/// <summary>
/// Builds a self-contained, offline-viewable HTML report of a startup impact session, styled to
/// resemble <see cref="DialogStartupImpact"/> as closely as reasonably possible, including its
/// hover highlighting, tooltips, mod filter, mod visibility toggles and logarithmic scale
/// controls. Everything (data, CSS and JS) is inlined into a single file so it can be shared
/// without any external dependencies.
/// </summary>
[HotSwappable]
internal static class StartupImpactHtmlExporter
{
    internal static string BuildReport(
        StartupImpactSessionData sessionData,
        StartupImpactSessionViewData viewData,
        IReadOnlyDictionary<string, Color> modCategoryColors,
        Color defaultColor,
        bool showBaseGameOffThreadImpact,
        bool secondsOnly
    )
    {
        var json = BuildDataJson(
            sessionData,
            viewData,
            modCategoryColors,
            defaultColor,
            showBaseGameOffThreadImpact,
            secondsOnly
        );
        return Template.Replace("/*__DATA_JSON__*/", json, StringComparison.Ordinal);
    }

    private static string BuildDataJson(
        StartupImpactSessionData sessionData,
        StartupImpactSessionViewData viewData,
        IReadOnlyDictionary<string, Color> modCategoryColors,
        Color defaultColor,
        bool showBaseGameOffThreadImpact,
        bool secondsOnly
    )
    {
        var sb = new StringBuilder();
        _ = sb.Append('{');

        AppendNumber(sb, "loadingTimeMs", sessionData.LoadingTime);
        _ = sb.Append(',');
        AppendNumber(sb, "timeToMenuMs", sessionData.TimeToMenu);
        _ = sb.Append(',');
        AppendRemainingByStage(sb, viewData, defaultColor);
        AppendNumber(sb, "windowMs", viewData.TotalWindow);
        _ = sb.Append(',');
        AppendNumber(sb, "remainingMs", viewData.RemainingLoadingTime);
        _ = sb.Append(',');

        AppendKey(sb, "secondsOnly");
        _ = sb.Append(secondsOnly ? "true," : "false,");

        if (
            sessionData.DefsParsed is int defsParsed
            && sessionData.PatchOperationsApplied is int patchOperationsApplied
            && sessionData.ModsLoaded is int modsLoaded
        )
        {
            _ = sb.Append("\"sessionStats\":{");
            AppendNumber(sb, "defsParsed", defsParsed);
            _ = sb.Append(',');
            AppendNumber(sb, "patchOperationsApplied", patchOperationsApplied);
            _ = sb.Append(',');
            AppendNumber(sb, "modsLoaded", modsLoaded);
            _ = sb.Append("},");
        }
        else
        {
            _ = sb.Append("\"sessionStats\":null,");
        }

        _ = sb.Append("\"totalCategories\":[");
        for (var i = 0; i < StartupImpactSessionViewData.CategoriesTotal.Length; i++)
        {
            var key = StartupImpactSessionViewData.CategoriesTotal[i];
            if (i > 0)
            {
                _ = sb.Append(',');
            }
            _ = sb.Append('{');
            AppendString(sb, "label", key.Translate());
            _ = sb.Append(',');
            AppendString(
                sb,
                "color",
                ColorToHex(modCategoryColors.TryGetValue(key, out var c) ? c : defaultColor)
            );
            _ = sb.Append('}');
        }
        _ = sb.Append("],");

        _ = sb.Append("\"baseGame\":{");
        AppendNumber(sb, "loadingTimeMs", viewData.BasegameLoadingTime);
        _ = sb.Append(',');
        AppendNumber(
            sb,
            "offThreadTotalImpactMs",
            showBaseGameOffThreadImpact ? viewData.OffThreadBasegameLoadingTime : 0f
        );
        _ = sb.Append(',');
        _ = sb.Append("\"segments\":[");
        var nonModCategories = viewData.CategoriesNonMods;
        var nonModMetrics = viewData.MetricsNonMods;
        for (var i = 0; i < nonModCategories.Count; i++)
        {
            if (i > 0)
            {
                _ = sb.Append(',');
            }
            var category = nonModCategories[i];
            _ = sb.Append('{');
            AppendString(sb, "label", StartupImpactProfilerUtil.TranslateCategory(category));
            _ = sb.Append(',');
            AppendString(
                sb,
                "color",
                ColorToHex(
                    viewData.CategoryColorsNonMods.TryGetValue(category, out var c2)
                        ? c2
                        : defaultColor
                )
            );
            _ = sb.Append(',');
            AppendNumber(sb, "valueMs", nonModMetrics[i]);
            _ = sb.Append('}');
        }
        _ = sb.Append("],");

        _ = sb.Append("\"offThreadSegments\":[");
        if (showBaseGameOffThreadImpact && viewData.OffThreadBasegameLoadingTime > 1f)
        {
            AppendSegments(
                sb,
                viewData.CategoriesNonMods,
                viewData.MetricsOffThreadNonMods,
                viewData.CategoryColorsNonMods,
                defaultColor
            );
        }
        _ = sb.Append(']');

        _ = sb.Append("},");

        _ = sb.Append("\"mods\":[");
        var categories = viewData.Categories;
        var firstMod = true;
        foreach (var modView in viewData.ModViewData)
        {
            if (!firstMod)
            {
                _ = sb.Append(',');
            }
            firstMod = false;

            _ = sb.Append('{');
            AppendString(sb, "name", modView.ModData.ModName);
            _ = sb.Append(',');
            AppendString(sb, "packageId", modView.ModData.ModPackageId);
            _ = sb.Append(',');
            AppendString(
                sb,
                "color",
                ColorToHex(StartupImpactProfilerUtil.HashColor(modView.ModData.ModPackageId))
            );
            _ = sb.Append(',');
            AppendNumber(sb, "totalImpactMs", modView.ModData.TotalImpact);
            _ = sb.Append(',');
            AppendNumber(sb, "offThreadTotalImpactMs", modView.ModData.OffThreadTotalImpact);
            _ = sb.Append(',');

            _ = sb.Append("\"metrics\":[");
            AppendSegments(sb, categories, modView.Metrics, modCategoryColors, defaultColor);
            _ = sb.Append("],");

            _ = sb.Append("\"offThreadMetrics\":[");
            if (modView.ModData.OffThreadTotalImpact > 1f)
            {
                AppendSegments(
                    sb,
                    categories,
                    modView.OffThreadMetrics,
                    modCategoryColors,
                    defaultColor
                );
            }
            _ = sb.Append(']');

            _ = sb.Append('}');
        }
        _ = sb.Append("],");

        AppendPhases(sb, viewData, modCategoryColors, defaultColor);
        _ = sb.Append(',');

        AppendString(
            sb,
            "generatedAt",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        );
        _ = sb.Append(',');

        AppendStrings(sb);

        _ = sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// Appends every mod's time per loading phase, as <c>[modIndex, onThreadMs, offThreadMs]</c>
    /// entries indexing into <c>mods</c>, so the report can sum only the mods that are neither
    /// hidden nor filtered out.
    /// </summary>
    private static void AppendPhases(
        StringBuilder sb,
        StartupImpactSessionViewData viewData,
        IReadOnlyDictionary<string, Color> modCategoryColors,
        Color defaultColor
    )
    {
        Dictionary<StartupImpactSessionModData, int> modIndices = [];
        for (var i = 0; i < viewData.ModViewData.Count; i++)
        {
            modIndices[viewData.ModViewData[i].ModData] = i;
        }

        _ = sb.Append("\"phases\":[");
        var firstPhase = true;
        foreach (var phase in StartupImpactPhaseViewData.FromMods(modIndices.Keys))
        {
            if (!firstPhase)
            {
                _ = sb.Append(',');
            }
            firstPhase = false;

            _ = sb.Append('{');
            AppendString(sb, "label", phase.Label);
            _ = sb.Append(',');
            AppendString(
                sb,
                "color",
                ColorToHex(modCategoryColors.TryGetValue(phase.Key, out var c) ? c : defaultColor)
            );
            _ = sb.Append(',');
            _ = sb.Append("\"mods\":[");
            for (var i = 0; i < phase.Mods.Count; i++)
            {
                if (i > 0)
                {
                    _ = sb.Append(',');
                }
                _ = sb.Append('[')
                    .Append(modIndices[phase.Mods[i]].ToString(CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(phase.Metrics[i].ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(
                        phase.OffThreadMetrics[i].ToString("0.###", CultureInfo.InvariantCulture)
                    )
                    .Append(']');
            }
            _ = sb.Append("]}");
        }
        _ = sb.Append(']');
    }

    private static void AppendStrings(StringBuilder sb)
    {
        _ = sb.Append("\"strings\":{");
        AppendString(
            sb,
            "reportTitle",
            "LoadingProgress.StartupImpact.HtmlReport.Title".Translate()
        );
        _ = sb.Append(',');
        AppendString(sb, "title", "LoadingProgress.StartupImpact.StartupTime".Translate());
        _ = sb.Append(',');
        AppendString(sb, "sessionStats", "LoadingProgress.StartupImpact.SessionStats".Translate());
        _ = sb.Append(',');
        AppendString(
            sb,
            "baseGameTitle",
            "LoadingProgress.StartupImpact.StartupNonmods".Translate()
        );
        _ = sb.Append(',');
        AppendString(sb, "modsTitle", "LoadingProgress.StartupImpact.StartupMods".Translate());
        _ = sb.Append(',');
        AppendString(
            sb,
            "logScaleLabel",
            "LoadingProgress.StartupImpact.LogarithmicScale".Translate()
        );
        _ = sb.Append(',');
        AppendString(
            sb,
            "logScaleTip",
            "LoadingProgress.StartupImpact.LogarithmicScale.Tip".Translate()
        );
        _ = sb.Append(',');
        AppendString(
            sb,
            "scaleDetailLabel",
            "LoadingProgress.StartupImpact.ScaleDetail".Translate()
        );
        _ = sb.Append(',');
        AppendString(
            sb,
            "scaleDetailTip",
            "LoadingProgress.StartupImpact.ScaleDetail.Tip".Translate()
        );
        _ = sb.Append(',');
        AppendString(
            sb,
            "toggleModVisibilityTip",
            "LoadingProgress.StartupImpact.ToggleModVisibility.Tip".Translate()
        );
        _ = sb.Append(',');
        AppendString(sb, "filterLabel", "LoadingProgress.StartupImpact.FilterMods".Translate());
        _ = sb.Append(',');
        AppendString(
            sb,
            "filterPlaceholder",
            "LoadingProgress.StartupImpact.FilterMods.Placeholder".Translate()
        );
        _ = sb.Append(',');
        AppendString(sb, "columnMod", "LoadingProgress.StartupImpact.ColumnName".Translate());
        _ = sb.Append(',');
        AppendString(sb, "columnImpact", "LoadingProgress.StartupImpact.ColumnImpact".Translate());
        _ = sb.Append(',');
        AppendString(sb, "columnPhase", "LoadingProgress.StartupImpact.ColumnPhase".Translate());
        _ = sb.Append(',');
        AppendString(sb, "groupByMod", "LoadingProgress.StartupImpact.GroupByMod".Translate());
        _ = sb.Append(',');
        AppendString(sb, "groupByPhase", "LoadingProgress.StartupImpact.GroupByPhase".Translate());
        _ = sb.Append(',');
        AppendString(sb, "groupingTip", "LoadingProgress.StartupImpact.Grouping.Tip".Translate());
        _ = sb.Append(',');
        AppendString(sb, "footer", "LoadingProgress.StartupImpact.HtmlReport.Footer".Translate());
        _ = sb.Append(',');
        AppendString(
            sb,
            "remainingTitle",
            "LoadingProgress.StartupImpact.StartupRemaining".Translate()
        );
        _ = sb.Append(',');
        AppendString(sb, "secondsFormat", "LoadingProgress.StartupImpact.Seconds".Translate());
        _ = sb.Append(',');
        AppendString(
            sb,
            "millisecondsFormat",
            "LoadingProgress.StartupImpact.Milliseconds".Translate()
        );
        _ = sb.Append('}');
    }

    private static void AppendSegments(
        StringBuilder sb,
        IReadOnlyList<string> categories,
        IReadOnlyList<float> metrics,
        IReadOnlyDictionary<string, Color> colors,
        Color defaultColor
    )
    {
        var first = true;
        for (var i = 0; i < categories.Count; i++)
        {
            var value = metrics[i];
            if (value <= 0)
            {
                continue;
            }
            if (!first)
            {
                _ = sb.Append(',');
            }
            first = false;

            _ = sb.Append('{');
            AppendString(sb, "label", StartupImpactProfilerUtil.TranslateCategory(categories[i]));
            _ = sb.Append(',');
            AppendString(
                sb,
                "color",
                ColorToHex(colors.TryGetValue(categories[i], out var c) ? c : defaultColor)
            );
            _ = sb.Append(',');
            AppendNumber(sb, "valueMs", value);
            _ = sb.Append('}');
        }
    }

    private static string ColorToHex(Color c) =>
        $"#{(int)Mathf.Round(Mathf.Clamp01(c.r) * 255):x2}"
        + $"{(int)Mathf.Round(Mathf.Clamp01(c.g) * 255):x2}"
        + $"{(int)Mathf.Round(Mathf.Clamp01(c.b) * 255):x2}";

    /// <summary>
    /// The remaining time by loading stage, largest first, as the segments of the report's
    /// remaining bar, so it can say where the time no category accounts for went.
    /// </summary>
    private static void AppendRemainingByStage(
        StringBuilder sb,
        StartupImpactSessionViewData viewData,
        Color defaultColor
    )
    {
        _ = sb.Append("\"remainingByStage\":[");
        var first = true;
        foreach (var entry in viewData.RemainingByStage)
        {
            if (!first)
            {
                _ = sb.Append(',');
            }
            first = false;
            _ = sb.Append('{');
            AppendString(sb, "label", entry.Label);
            _ = sb.Append(',');
            AppendString(
                sb,
                "color",
                ColorToHex(
                    viewData.CategoryColorsRemaining.TryGetValue(entry.Label, out var c)
                        ? c
                        : defaultColor
                )
            );
            _ = sb.Append(',');
            AppendNumber(sb, "valueMs", entry.Ms);
            _ = sb.Append('}');
        }
        _ = sb.Append("],");
    }

    private static void AppendNumber(StringBuilder sb, string key, float value)
    {
        AppendKey(sb, key);
        _ = sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static void AppendNumber(StringBuilder sb, string key, int value)
    {
        AppendKey(sb, key);
        _ = sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendString(StringBuilder sb, string key, string value)
    {
        AppendKey(sb, key);
        AppendJsonString(sb, value);
    }

    private static void AppendKey(StringBuilder sb, string key)
    {
        AppendJsonString(sb, key);
        _ = sb.Append(':');
    }

    private static void AppendJsonString(StringBuilder sb, string value)
    {
        _ = sb.Append('"');
        foreach (var ch in value)
        {
            var escaped = ch switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < (char)0x20 => "\\u" + ((int)ch).ToString("x4", CultureInfo.InvariantCulture),
                _ => ch.ToString(),
            };
            _ = sb.Append(escaped);
        }
        _ = sb.Append('"');
    }

    private const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Startup Impact Report</title>
<style>
  :root {
    --bg-page: #060607;
    --bg-panel: #0c0c0e;
    --border: #2c2820;
    --text: #e4e1d8;
    --text-dim: #9d9a8f;
    --button-bg: #4b3f2a;
    --button-bg-hover: #5c4d33;
    --button-border: #8a7048;
    --track-bg: #201f1c;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    padding: 24px 12px;
    background: var(--bg-page);
    color: var(--text);
    font-family: "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    font-size: 14px;
  }
  .window {
    max-width: 800px;
    margin: 0 auto;
    background: var(--bg-panel);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 16px 20px 20px;
    box-shadow: 0 12px 40px rgba(0, 0, 0, 0.6);
  }
  h1, h2 {
    margin: 0;
    font-weight: 600;
    color: var(--text);
  }
  h1 { font-size: 20px; }
  h2 { font-size: 16px; margin-top: 18px; }
  .titlebar-row {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 16px;
    flex-wrap: wrap;
  }
  .logscale-controls {
    display: flex;
    align-items: center;
    gap: 10px;
    flex-wrap: wrap;
    padding-top: 2px;
  }
  .logscale-controls label {
    display: flex;
    align-items: center;
    gap: 6px;
    color: var(--text-dim);
    font-size: 13px;
    cursor: pointer;
    white-space: nowrap;
  }
  .slider-wrap {
    display: flex;
    align-items: center;
    gap: 8px;
  }
  input[type="range"] {
    width: 140px;
    accent-color: var(--button-border);
  }
  input[type="checkbox"] {
    accent-color: var(--button-border);
    width: 15px;
    height: 15px;
  }
  .bar {
    position: relative;
    display: flex;
    height: 38px;
    margin-top: 6px;
    background: var(--track-bg);
    border: 1px solid var(--border);
    border-radius: 3px;
    overflow: hidden;
  }
  .segment {
    height: 100%;
    background: var(--seg-color);
    cursor: default;
    transition: filter 0.08s ease-out;
  }
  .segment:hover {
    filter: brightness(1.28);
    box-shadow: inset 0 0 0 2px rgba(255, 255, 255, 0.2);
  }
  .session-stats {
    margin-top: 10px;
    color: var(--text-dim);
    font-size: 13px;
  }
  .filter-row {
    display: flex;
    align-items: center;
    gap: 8px;
    margin: 10px 0 10px;
  }
  .filter-row label { color: var(--text-dim); white-space: nowrap; }
  .filter-row input[type="text"] {
    flex: 1;
    background: var(--track-bg);
    border: 1px solid var(--border);
    color: var(--text);
    padding: 6px 8px;
    border-radius: 3px;
    font-size: 13px;
  }
  .filter-row .group-toggle {
    background: var(--button-bg);
    border: 1px solid var(--button-border);
    color: var(--text);
    padding: 5px 10px;
    border-radius: 3px;
    font-size: 13px;
    white-space: nowrap;
    cursor: pointer;
  }
  .filter-row .group-toggle:hover { background: var(--button-bg-hover); }
  .table-header {
    display: grid;
    grid-template-columns: 40px 30fr 80px 38fr;
    align-items: center;
    padding: 0 6px;
    gap: 6px;
    margin-top: 4px;
    color: var(--text-dim);
    font-size: 13px;
  }
  .table-header .sort-col {
    cursor: pointer;
    user-select: none;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .table-header .sort-col:hover { color: var(--text); }
  .table-header [data-col="impact"] {
    text-align: right;
    padding-right: 4px;
  }
  .table {
    border: 1px solid var(--border);
    border-radius: 3px;
    max-height: 420px;
    overflow-y: auto;
  }
  .row {
    display: grid;
    grid-template-columns: 40px 30fr 80px 38fr;
    align-items: center;
    height: 40px;
    border-bottom: 1px solid var(--border);
    padding: 0 6px;
    gap: 6px;
  }
  .row:last-child { border-bottom: none; }
  .row.hidden-mod { filter: brightness(0.55); }
  .row .eye {
    background: none;
    border: none;
    color: var(--text);
    font-size: 16px;
    cursor: pointer;
    width: 28px;
    height: 28px;
    border-radius: 3px;
    line-height: 1;
  }
  .row .eye:hover { background: rgba(255, 255, 255, 0.08); }
  .row .swatch {
    width: 20px;
    height: 20px;
    margin: 0 auto;
    background: var(--seg-color);
  }
  .row .mod-name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .row .mod-time {
    color: var(--text-dim);
    text-align: right;
    padding-right: 4px;
    white-space: nowrap;
  }
  .row .mod-bar-cell {
    display: flex;
    flex-direction: column;
    height: 28px;
    border-radius: 3px;
    overflow: hidden;
    border: 1px solid var(--border);
  }
  .row .mod-bar-cell .bar {
    flex: 1;
    margin: 0;
    border: none;
    border-radius: 0;
    height: auto;
    min-height: 0;
  }
  .basegame-bar-cell {
    display: flex;
    flex-direction: column;
    height: 38px;
    margin-top: 6px;
    border: 1px solid var(--border);
    border-radius: 3px;
    overflow: hidden;
  }
  .basegame-bar-cell .bar {
    flex: 1;
    margin: 0;
    border: none;
    border-radius: 0;
    height: auto;
    min-height: 0;
  }
  .tooltip {
    position: fixed;
    display: none;
    max-width: 320px;
    background: #1c1a16;
    border: 1px solid var(--button-border);
    color: var(--text);
    padding: 6px 9px;
    border-radius: 4px;
    font-size: 12px;
    line-height: 1.4;
    pointer-events: none;
    z-index: 1000;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.5);
  }
  .footer {
    margin-top: 18px;
    padding-top: 12px;
    border-top: 1px solid var(--border);
    color: var(--text-dim);
    font-size: 12px;
    line-height: 1.5;
  }
</style>
</head>
<body>
<div class="window">
  <div class="titlebar-row">
    <h1 id="title"></h1>
    <div class="logscale-controls">
      <div class="slider-wrap" id="sliderWrap" style="display:none">
        <label for="tauSlider" id="sliderLabel"></label>
        <input type="range" id="tauSlider" min="100" max="5000" step="50" value="1000">
      </div>
      <label id="logCheckboxWrap">
        <input type="checkbox" id="logToggle">
        <span id="logScaleLabelText"></span>
      </label>
    </div>
  </div>

  <div class="bar" id="totalBar"></div>
  <div class="session-stats" id="sessionStats"></div>

  <h2 id="baseGameTitle"></h2>
  <div class="basegame-bar-cell" id="baseGameBar"></div>

  <h2 id="remainingTitle" style="display:none"></h2>
  <div class="bar" id="remainingBar" style="display:none"></div>

  <h2 id="modsTitle"></h2>
  <div class="bar" id="modsBar"></div>
  <div class="filter-row">
    <label for="filterInput" id="filterLabelText"></label>
    <input type="text" id="filterInput">
    <button type="button" class="group-toggle" id="groupToggle"></button>
  </div>
  <div class="table-header" id="tableHeader">
    <div></div>
    <div class="sort-col" data-col="name"></div>
    <div class="sort-col" data-col="impact"></div>
    <div></div>
  </div>
  <div class="table" id="modsTable"></div>

  <div class="footer" id="footer"></div>
</div>
<div class="tooltip" id="tooltip"></div>

<script>
(function () {
  "use strict";

  var DATA = /*__DATA_JSON__*/;

  document.title = DATA.strings.reportTitle;
  document.getElementById("sliderLabel").setAttribute("data-tip", DATA.strings.scaleDetailTip);
  document.getElementById("logCheckboxWrap").setAttribute("data-tip", DATA.strings.logScaleTip);
  document.getElementById("logScaleLabelText").textContent = DATA.strings.logScaleLabel;
  document.getElementById("filterLabelText").textContent = DATA.strings.filterLabel;
  document.getElementById("filterInput").setAttribute("placeholder", DATA.strings.filterPlaceholder);
  document.getElementById("groupToggle").setAttribute("data-tip", DATA.strings.groupingTip);

  var state = {
    useLog: false,
    tau: 1000,
    filter: "",
    hidden: new Set(),
    sortColumn: "impact",
    sortAscending: false,
    groupByPhase: false,
    phaseSortColumn: "impact",
    phaseSortAscending: false
  };

  var tooltipEl = document.getElementById("tooltip");

  function clockText(ms) {
    var tenths = Math.round(ms / 100);
    if (tenths < 600) {
      return null;
    }
    var hours = Math.floor(tenths / 36000);
    var minutes = Math.floor(tenths / 600) % 60;
    var seconds = Math.floor(tenths / 10) % 60;
    var tenth = tenths % 10;
    var ss = (seconds < 10 ? "0" : "") + seconds + "." + tenth;
    if (hours > 0) {
      return hours + ":" + (minutes < 10 ? "0" : "") + minutes + ":" + ss;
    }
    return minutes + ":" + ss;
  }

  function timeText(ms) {
    var clock = DATA.secondsOnly ? null : clockText(ms);
    if (clock !== null) {
      return clock;
    }
    if (ms > 10000) {
      return DATA.strings.secondsFormat.replace("{0}", (ms / 1000).toFixed(1));
    }
    if (ms > 1000) {
      return DATA.strings.secondsFormat.replace("{0}", (ms / 1000).toFixed(2));
    }
    return DATA.strings.millisecondsFormat.replace("{0}", Math.round(ms));
  }

  function logScaleTransform(x, tau) {
    return Math.log10(1 + Math.max(0, x) / tau);
  }

  function logShares(values, tau) {
    var shares = values.map(function (v) { return logScaleTransform(v, tau); });
    var sum = shares.reduce(function (a, b) { return a + b; }, 0);
    if (sum > 0) {
      shares = shares.map(function (s) { return s / sum; });
      var lastNonZero = -1;
      for (var i = shares.length - 1; i >= 0; i--) {
        if (shares[i] > 0) { lastNonZero = i; break; }
      }
      if (lastNonZero >= 0) {
        var actualSum = shares.reduce(function (a, b) { return a + b; }, 0);
        shares[lastNonZero] += Math.max(0, 1 - actualSum);
      }
    }
    return shares;
  }

  function showTooltip(evt, text) {
    tooltipEl.textContent = text;
    tooltipEl.style.display = "block";
    moveTooltip(evt);
  }

  function moveTooltip(evt) {
    var x = evt.clientX + 14;
    var y = evt.clientY + 14;
    var maxX = window.innerWidth - tooltipEl.offsetWidth - 8;
    var maxY = window.innerHeight - tooltipEl.offsetHeight - 8;
    tooltipEl.style.left = Math.min(x, Math.max(0, maxX)) + "px";
    tooltipEl.style.top = Math.min(y, Math.max(0, maxY)) + "px";
  }

  function hideTooltip() {
    tooltipEl.style.display = "none";
  }

  // Plain, static tooltips for controls (checkbox/slider/eye icon) don't need to track the
  // mouse continuously; wire them once up front.
  document.querySelectorAll("[data-tip]").forEach(function (el) {
    el.addEventListener("mouseenter", function (evt) { showTooltip(evt, el.getAttribute("data-tip")); });
    el.addEventListener("mousemove", moveTooltip);
    el.addEventListener("mouseleave", hideTooltip);
  });

  function renderBar(container, segments, maxImpactMs) {
    container.innerHTML = "";
    var values = segments.map(function (s) { return Math.max(0, s.valueMs); });
    var sumLinear = values.reduce(function (a, b) { return a + b; }, 0);
    if (sumLinear <= 0) {
      return;
    }

    function appendSegment(seg, widthPercent) {
      if (widthPercent <= 0) {
        return;
      }
      var el = document.createElement("div");
      el.className = "segment";
      el.style.setProperty("--seg-color", seg.color);
      el.style.width = widthPercent + "%";
      var tipText = seg.label + ": " + timeText(seg.valueMs);
      el.addEventListener("mouseenter", function (evt) { showTooltip(evt, tipText); });
      el.addEventListener("mousemove", moveTooltip);
      el.addEventListener("mouseleave", hideTooltip);
      container.appendChild(el);
    }

    if (!state.useLog) {
      segments.forEach(function (seg, i) {
        if (values[i] <= 0) { return; }
        appendSegment(seg, 100 * values[i] / Math.max(1, maxImpactMs));
      });
      return;
    }

    var denomCap = logScaleTransform(maxImpactMs, state.tau);
    var barFill = denomCap > 0 ? logScaleTransform(sumLinear, state.tau) / denomCap : 0;
    barFill = Math.min(1, Math.max(0, barFill));

    var shares = logShares(values, state.tau);
    var lastIdx = -1;
    for (var i = segments.length - 1; i >= 0; i--) {
      if (values[i] > 0 && shares[i] > 0) { lastIdx = i; break; }
    }

    var drawn = 0;
    segments.forEach(function (seg, i) {
      if (values[i] <= 0 || shares[i] <= 0) { return; }
      var widthFrac = barFill * shares[i];
      if (i === lastIdx) {
        widthFrac = Math.max(0, barFill - drawn);
      }
      appendSegment(seg, widthFrac * 100);
      drawn += widthFrac;
    });
  }

  // Both of a row's bars on one scale, shared with the other rows; a hidden mod larger than
  // the shared scale gets its own.
  function modMaxImpact(mod, sessionMaxImpact) {
    var own = Math.max(mod.totalImpactMs, mod.offThreadTotalImpactMs);
    return state.hidden.has(mod) ? Math.max(sessionMaxImpact, own) : sessionMaxImpact;
  }

  function computeSessionMaxImpact() {
    var max = 0;
    DATA.mods.forEach(function (mod) {
      if (!state.hidden.has(mod)) {
        max = Math.max(max, mod.totalImpactMs, mod.offThreadTotalImpactMs);
      }
    });
    return max;
  }

  function renderTotalBar() {
    var modsTotal = 0;
    var hiddenTotal = 0;
    DATA.mods.forEach(function (mod) {
      if (state.hidden.has(mod)) {
        hiddenTotal += mod.totalImpactMs;
      } else {
        modsTotal += mod.totalImpactMs;
      }
    });
    var baseGameTotal = DATA.baseGame.loadingTimeMs;
    // The window's own span: the time to the menu when the session recorded one, and never
    // less than the timed steps.
    var windowMs = DATA.windowMs;

    var cats = DATA.totalCategories;
    var segments = [
      { label: cats[0].label, color: cats[0].color, valueMs: modsTotal },
      { label: cats[1].label, color: cats[1].color, valueMs: hiddenTotal },
      { label: cats[2].label, color: cats[2].color, valueMs: baseGameTotal },
      { label: cats[3].label, color: cats[3].color, valueMs: DATA.remainingMs }
    ];
    renderBar(document.getElementById("totalBar"), segments, windowMs);
    document.getElementById("modsTitle").textContent = DATA.strings.modsTitle.replace("{0}", timeText(modsTotal));
  }

  function renderModRow(mod, sessionMaxImpact) {
    var row = document.createElement("div");
    row.className = "row" + (state.hidden.has(mod) ? " hidden-mod" : "");

    var eye = document.createElement("button");
    eye.className = "eye";
    eye.type = "button";
    eye.textContent = state.hidden.has(mod) ? "–" : "◉";
    eye.setAttribute("data-tip", DATA.strings.toggleModVisibilityTip);
    eye.addEventListener("mouseenter", function (evt) { showTooltip(evt, eye.getAttribute("data-tip")); });
    eye.addEventListener("mousemove", moveTooltip);
    eye.addEventListener("mouseleave", hideTooltip);
    eye.addEventListener("click", function () {
      if (state.hidden.has(mod)) { state.hidden.delete(mod); } else { state.hidden.add(mod); }
      renderAll();
    });
    row.appendChild(eye);

    var name = document.createElement("div");
    name.className = "mod-name";
    name.textContent = mod.name;
    name.title = mod.name + " (" + mod.packageId + ")";
    row.appendChild(name);

    var time = document.createElement("div");
    time.className = "mod-time";
    time.textContent = timeText(mod.totalImpactMs);
    row.appendChild(time);

    var barCell = document.createElement("div");
    barCell.className = "mod-bar-cell";
    var rowMaxImpact = modMaxImpact(mod, sessionMaxImpact);
    if (mod.offThreadTotalImpactMs > 1) {
      var offBar = document.createElement("div");
      offBar.className = "bar";
      renderBar(offBar, mod.offThreadMetrics, rowMaxImpact);
      barCell.appendChild(offBar);
    }
    var mainBar = document.createElement("div");
    mainBar.className = "bar";
    renderBar(mainBar, mod.metrics, rowMaxImpact);
    barCell.appendChild(mainBar);
    row.appendChild(barCell);

    return row;
  }

  function compareMods(a, b) {
    var cmp;
    if (state.sortColumn === "name") {
      var aName = a.name.toLowerCase();
      var bName = b.name.toLowerCase();
      cmp = aName < bName ? -1 : aName > bName ? 1 : 0;
    } else {
      cmp = a.totalImpactMs - b.totalImpactMs;
    }
    return state.sortAscending ? cmp : -cmp;
  }

  function matchesFilter(mod) {
    var filter = state.filter.trim().toLowerCase();
    return (
      !filter
      || mod.name.toLowerCase().indexOf(filter) >= 0
      || mod.packageId.toLowerCase().indexOf(filter) >= 0
    );
  }

  function renderModsTable() {
    var container = document.getElementById("modsTable");
    container.innerHTML = "";
    var sessionMaxImpact = computeSessionMaxImpact();
    DATA.mods
      .filter(matchesFilter)
      .sort(compareMods)
      .forEach(function (mod) {
        container.appendChild(renderModRow(mod, sessionMaxImpact));
      });
  }

  // Sums each phase over the mods that are neither hidden nor filtered out.
  function computePhaseRows() {
    var rows = [];
    DATA.phases.forEach(function (phase) {
      var segments = [];
      var offThreadSegments = [];
      var total = 0;
      var offThreadTotal = 0;
      phase.mods.forEach(function (entry) {
        var mod = DATA.mods[entry[0]];
        if (state.hidden.has(mod) || !matchesFilter(mod)) {
          return;
        }
        segments.push({ label: mod.name, color: mod.color, valueMs: entry[1] });
        offThreadSegments.push({ label: mod.name, color: mod.color, valueMs: entry[2] });
        total += entry[1];
        offThreadTotal += entry[2];
      });
      if (total > 0 || offThreadTotal > 0) {
        rows.push({
          label: phase.label,
          color: phase.color,
          totalImpactMs: total,
          offThreadTotalImpactMs: offThreadTotal,
          segments: segments,
          offThreadSegments: offThreadSegments
        });
      }
    });
    return rows;
  }

  function comparePhases(a, b) {
    var cmp;
    if (state.phaseSortColumn === "name") {
      var aLabel = a.label.toLowerCase();
      var bLabel = b.label.toLowerCase();
      cmp = aLabel < bLabel ? -1 : aLabel > bLabel ? 1 : 0;
    } else {
      cmp = a.totalImpactMs - b.totalImpactMs;
    }
    return state.phaseSortAscending ? cmp : -cmp;
  }

  function renderPhaseRow(phase, maxImpact) {
    var row = document.createElement("div");
    row.className = "row";

    var swatch = document.createElement("div");
    swatch.className = "swatch";
    swatch.style.setProperty("--seg-color", phase.color);
    row.appendChild(swatch);

    var name = document.createElement("div");
    name.className = "mod-name";
    name.textContent = phase.label;
    name.title = phase.label;
    row.appendChild(name);

    var time = document.createElement("div");
    time.className = "mod-time";
    time.textContent = timeText(phase.totalImpactMs);
    row.appendChild(time);

    var barCell = document.createElement("div");
    barCell.className = "mod-bar-cell";
    if (phase.offThreadTotalImpactMs > 1) {
      var offBar = document.createElement("div");
      offBar.className = "bar";
      renderBar(offBar, phase.offThreadSegments, maxImpact);
      barCell.appendChild(offBar);
    }
    var mainBar = document.createElement("div");
    mainBar.className = "bar";
    renderBar(mainBar, phase.segments, maxImpact);
    barCell.appendChild(mainBar);
    row.appendChild(barCell);

    return row;
  }

  function renderPhasesTable() {
    var container = document.getElementById("modsTable");
    container.innerHTML = "";
    var rows = computePhaseRows();
    var maxImpact = rows.reduce(function (max, phase) {
      return Math.max(max, phase.totalImpactMs, phase.offThreadTotalImpactMs);
    }, 0);
    rows.sort(comparePhases).forEach(function (phase) {
      container.appendChild(renderPhaseRow(phase, maxImpact));
    });
  }

  function renderTable() {
    if (state.groupByPhase) {
      renderPhasesTable();
    } else {
      renderModsTable();
    }
  }

  function updateSortHeader() {
    var sortColumn = state.groupByPhase ? state.phaseSortColumn : state.sortColumn;
    var sortAscending = state.groupByPhase ? state.phaseSortAscending : state.sortAscending;
    document.querySelectorAll(".sort-col").forEach(function (el) {
      var col = el.getAttribute("data-col");
      var label = col !== "name"
        ? DATA.strings.columnImpact
        : state.groupByPhase ? DATA.strings.columnPhase : DATA.strings.columnMod;
      if (sortColumn === col) {
        label += " " + (sortAscending ? "▲" : "▼");
      }
      el.textContent = label;
    });
  }

  document.querySelectorAll(".sort-col").forEach(function (el) {
    el.addEventListener("click", function () {
      var col = el.getAttribute("data-col");
      var columnKey = state.groupByPhase ? "phaseSortColumn" : "sortColumn";
      var ascendingKey = state.groupByPhase ? "phaseSortAscending" : "sortAscending";
      if (state[columnKey] === col) {
        state[ascendingKey] = !state[ascendingKey];
      } else {
        state[columnKey] = col;
        state[ascendingKey] = col === "name";
      }
      updateSortHeader();
      renderTable();
    });
  });
  updateSortHeader();

  var groupToggle = document.getElementById("groupToggle");
  function updateGroupToggle() {
    groupToggle.textContent = state.groupByPhase ? DATA.strings.groupByPhase : DATA.strings.groupByMod;
  }
  updateGroupToggle();
  groupToggle.addEventListener("click", function () {
    state.groupByPhase = !state.groupByPhase;
    updateGroupToggle();
    updateSortHeader();
    renderTable();
  });

  function renderBaseGameBar() {
    var container = document.getElementById("baseGameBar");
    container.innerHTML = "";
    var maxImpact = Math.max(DATA.baseGame.loadingTimeMs, DATA.baseGame.offThreadTotalImpactMs);
    if (DATA.baseGame.offThreadTotalImpactMs > 1) {
      var offBar = document.createElement("div");
      offBar.className = "bar";
      renderBar(offBar, DATA.baseGame.offThreadSegments, maxImpact);
      container.appendChild(offBar);
    }
    var mainBar = document.createElement("div");
    mainBar.className = "bar";
    // Both bars on one scale: the longer spans the width, the other is drawn in proportion.
    renderBar(mainBar, DATA.baseGame.segments, maxImpact);
    container.appendChild(mainBar);
  }

  function renderModsBar() {
    var visibleMods = DATA.mods.filter(function (mod) { return !state.hidden.has(mod); });
    var total = visibleMods.reduce(function (sum, mod) { return sum + mod.totalImpactMs; }, 0);
    var segments = visibleMods
      .slice()
      .sort(function (a, b) { return b.totalImpactMs - a.totalImpactMs; })
      .map(function (mod) { return { label: mod.name, color: mod.color, valueMs: mod.totalImpactMs }; });
    renderBar(document.getElementById("modsBar"), segments, total);
  }

  // The remaining time has no owner, so hiding a mod changes none of it; the bar is still
  // redrawn with the rest so the log scale applies to it too.
  function renderRemainingBar() {
    if (!DATA.remainingByStage.length) {
      return;
    }
    var title = document.getElementById("remainingTitle");
    title.textContent = DATA.strings.remainingTitle.replace("{0}", timeText(DATA.remainingMs));
    title.style.display = "";
    var bar = document.getElementById("remainingBar");
    bar.style.display = "";
    renderBar(bar, DATA.remainingByStage, DATA.remainingMs);
  }

  function renderAll() {
    renderTotalBar();
    renderBaseGameBar();
    renderRemainingBar();
    renderModsBar();
    renderTable();
  }

  document.getElementById("title").textContent =
    DATA.strings.title.replace("{0}", timeText(DATA.windowMs));

  if (DATA.sessionStats) {
    document.getElementById("sessionStats").textContent = DATA.strings.sessionStats
      .replace("{0}", DATA.sessionStats.defsParsed.toLocaleString())
      .replace("{1}", DATA.sessionStats.patchOperationsApplied.toLocaleString())
      .replace("{2}", DATA.sessionStats.modsLoaded.toLocaleString());
  }

  document.getElementById("baseGameTitle").textContent =
    DATA.strings.baseGameTitle.replace("{0}", timeText(DATA.baseGame.loadingTimeMs));

  document.getElementById("footer").textContent =
    DATA.strings.footer.replace("{0}", DATA.generatedAt);

  var logToggle = document.getElementById("logToggle");
  var sliderWrap = document.getElementById("sliderWrap");
  var tauSlider = document.getElementById("tauSlider");
  var sliderLabel = document.getElementById("sliderLabel");
  var filterInput = document.getElementById("filterInput");

  function updateSliderLabel() {
    sliderLabel.textContent = DATA.strings.scaleDetailLabel.replace("{0}", timeText(state.tau));
  }
  updateSliderLabel();

  logToggle.addEventListener("change", function () {
    state.useLog = logToggle.checked;
    sliderWrap.style.display = state.useLog ? "flex" : "none";
    renderAll();
  });
  tauSlider.addEventListener("input", function () {
    state.tau = parseFloat(tauSlider.value);
    updateSliderLabel();
    renderAll();
  });
  filterInput.addEventListener("input", function () {
    state.filter = filterInput.value;
    renderTable();
  });

  renderAll();
})();
</script>
</body>
</html>
""";
}
