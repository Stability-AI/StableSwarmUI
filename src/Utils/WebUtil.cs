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
}
