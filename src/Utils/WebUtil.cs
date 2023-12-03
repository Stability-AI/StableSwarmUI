using FreneticUtilities.FreneticExtensions;
using Microsoft.AspNetCore.Html;

namespace StableSwarmUI.Utils;

/// <summary>Helper utilities for web content generation.</summary>
public static class WebUtil
{
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

    public static HtmlString ModalHeader(string id, string title)
    {
        return new($"""
            <div class="modal" tabindex="-1" role="dialog" id="{id}">
                <div class="modal-dialog" role="document">
                    <div class="modal-content">
                        <div class="modal-header"><h5 class="modal-title">{title}</h5></div>
            """);
    }

    public static HtmlString ModalFooter() => new("</div></div></div>");

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

    /// <summary>Returns a short string identifying whether the user's GPU is good enough.</summary>
    public static HtmlString CheckGPUIsSufficient()
    {
        NvidiaUtil.NvidiaInfo[] nv = NvidiaUtil.QueryNvidia();
        if (nv is null || nv.IsEmpty())
        {
            return new("Unknown GPU.");
        }
        NvidiaUtil.NvidiaInfo bestGpu = nv.OrderByDescending(x => x.TotalMemory.InBytes).First();
        string basic = "";
        foreach (NvidiaUtil.NvidiaInfo info in nv)
        {
            basic += $"{(info == bestGpu ? "* " : "")}GPU {info.ID}: <b>{info.GPUName}</b>, <b>{info.TotalMemory}</b> VRAM\n<br>";
        }
        if (nv.Length > 1)
        {
            basic += $"({nv.Length} total GPUs) ";
        }
        if (bestGpu.TotalMemory.GiB > 15)
        {
            return new($"{basic}able to run locally for almost anything.");
        }
        if (bestGpu.TotalMemory.GiB > 11)
        {
            return new($"{basic}sufficient to run most usages locally.");
        }
        if (bestGpu.TotalMemory.GiB > 7)
        {
            return new($"{basic}sufficient to run basic usage locally. May be limited on large generations.");
        }
        if (bestGpu.TotalMemory.GiB > 3)
        {
            return new($"{basic}limited, may need to configure settings for LowVRAM usage to work reliably.");
        }
        return new($"{basic}insufficient, may work with LowVRAM or CPU mode, but otherwise will need remote cloud process.");
    }
}
