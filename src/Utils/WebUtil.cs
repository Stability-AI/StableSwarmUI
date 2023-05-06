using Microsoft.AspNetCore.Html;

namespace StableUI.Utils;

public static class WebUtil
{
    public static HtmlString AutoSlider(string name, string description, string id, double min, double max, double value, double step = 1)
    {
        return new HtmlString($"""
<div class="auto-slider-box">
<span class="auto-slider-name">{name}</span> <span class="auto-slider-description">{description}</span> <input class="auto-slider-number" type="number" value="{value}" min="{min}" max="{max}" step="{step}">
<br>
<input class="auto-slider-range" type="range" id="{id}" value="{value}" min="{min}" max="{max}" step="{step}">
</div>
"""
            );
    }
}
