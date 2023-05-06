using Microsoft.AspNetCore.Html;

namespace StableUI.Utils;

/// <summary>Helper utilities for web content generation.</summary>
public static class WebUtil
{
    /// <summary>Generates a clean number slider input block.</summary>
    public static HtmlString AutoSlider(string name, string description, string id, double min, double max, double value, double step = 1)
    {
        name = HtmlEscape(name);
        description = HtmlEscape(description);
        return new HtmlString($"""
<div class="auto-input auto-slider-box" title="{name}: {description}">
    <div class="auto-input-fade-lock auto-slider-fade-contain">
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
    public static HtmlString AutoNumber(string name, string description, string id, double min, double max, double value, double step = 1)
    {
        name = HtmlEscape(name);
        description = HtmlEscape(description);
        return new HtmlString($"""
<div class="auto-input auto-number-box" title="{name}: {description}">
    <div class="auto-input-fade-lock auto-fade-max-contain">
        <span class="auto-input-name">{name}</span> <span class="auto-input-description">{description}</span>
    </div>
    <input class="auto-number" type="number" id="{id}" value="{value}" min="{min}" max="{max}" step="{step}">
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
