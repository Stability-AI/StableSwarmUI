using Microsoft.AspNetCore.Html;

namespace StableUI.Utils;

/// <summary>Helper utilities for web content generation.</summary>
public static class WebUtil
{
    /// <summary>Generates a clean number slider input block.</summary>
    public static HtmlString AutoSlider(string featureid, string name, string description, string id, double min, double max, double value, double step = 1)
    {
        string js = $"{JSStringEscape(name)}: {JSStringEscape(description)}";
        name = HtmlEscape(name);
        description = HtmlEscape(description);
        return new HtmlString($"""
<div class="auto-input auto-slider-box" title="{name}: {description}" data-feature-require="{featureid}">
    <div class="auto-input-fade-lock auto-slider-fade-contain" onclick="alert('{js}')">
        <span class="auto-input-name">{name}</span> <span class="auto-input-description">{description}</span>
    </div>
    <input class="auto-slider-number" type="number" value="{value}" min="{min}" max="{max}" step="{step}">
    <br>
    <input class="auto-slider-range" type="range" id="{id}" value="{value}" min="{min}" max="{max}" step="{step}">
</div>
"""
            );
    }

    /// <summary>Generates a clean number input block.</summary>
    public static HtmlString AutoNumber(string featureid, string name, string description, string id, double min, double max, double value, double step = 1)
    {
        string js = $"{JSStringEscape(name)}: {JSStringEscape(description)}";
        name = HtmlEscape(name);
        description = HtmlEscape(description);
        return new HtmlString($"""
<div class="auto-input auto-number-box" title="{name}: {description}" data-feature-require="{featureid}">
    <div class="auto-input-fade-lock auto-fade-max-contain" onclick="alert('{js}')">
        <span class="auto-input-name">{name}</span> <span class="auto-input-description">{description}</span>
    </div>
    <input class="auto-number" type="number" id="{id}" value="{value}" min="{min}" max="{max}" step="{step}">
</div>
""");
    }

    /// <summary>Generates a clean number input block.</summary>
    public static HtmlString AutoNumberSmall(string featureid, string name, string description, string id, double min, double max, double value, double step = 1)
    {
        string js = $"{JSStringEscape(name)}: {JSStringEscape(description)}";
        name = HtmlEscape(name);
        description = HtmlEscape(description);
        return new HtmlString($"""
<div class="auto-input auto-number-box" title="{name}: {description}" data-feature-require="{featureid}">
    <span class="auto-input-name" onclick="alert('{js}')">{name}</span>
    <input class="auto-number-small" type="number" id="{id}" value="{value}" min="{min}" max="{max}" step="{step}">
</div>
""");
    }

    public static HtmlString Toast(string box_id, string header, string small_side, string content_id, string content, bool show)
    {
        return new HtmlString($"""
<div class="toast {(show ? "show" : "hide")}" role="alert" aria-live="assertive" aria-atomic="true" id="{box_id}">
    <div class="toast-header">
    <strong class="me-auto">{header}</strong>
    <small>{small_side}</small>
    <button type="button" class="btn-close ms-2 mb-1" data-bs-dismiss="toast" aria-label="Close">
        <span aria-hidden="true"></span>
    </button>
    </div>
    <div class="toast-body" id="{content_id}">
        {content}
    </div>
</div>
""");
    }

    /// <summary>Escapes a string for safe usage inside HTML blocks.</summary>
    public static string HtmlEscape(string str)
    {
        return str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    /// <summary>Escapes a string for safe usage inside JavaScript strings.</summary>
    public static string JSStringEscape(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}
