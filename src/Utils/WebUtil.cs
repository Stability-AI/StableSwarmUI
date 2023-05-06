using Microsoft.AspNetCore.Html;

namespace StableUI.Utils;

public static class WebUtil
{
    public static HtmlString AutoSlider(string name, string description, string id, double min, double max, double value, double step = 1)
    {
        name = HtmlEscape(name);
        description = HtmlEscape(description);
        return new HtmlString($"""
<div class="auto-input auto-slider-box" title="{name}: {description}">
<span class="auto-input-name">{name}</span> <span class="auto-input-description">{description}</span> <input class="auto-slider-number" type="number" value="{value}" min="{min}" max="{max}" step="{step}">
<br>
<input class="auto-slider-range" type="range" id="{id}" value="{value}" min="{min}" max="{max}" step="{step}">
</div>
"""
            );
    }

    public static HtmlString AutoNumber(string name, string description, string id, double min, double max, double value, double step = 1)
    {
        name = HtmlEscape(name);
        description = HtmlEscape(description);
        return new HtmlString($"""
<div class="auto-input auto-number-box" title="{name}: {description}">
<span class="auto-input-name">{name}</span> <span class="auto-input-description">{description}</span>
<br>
<input class="auto-number" type="number" id="{id}" value="{value}" min="{min}" max="{max}" step="{step}">
</div>
""");
    }

    public static string HtmlEscape(string str)
    {
        return str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
