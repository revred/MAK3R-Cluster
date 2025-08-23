using Microsoft.AspNetCore.Components;

namespace MAK3R.UI.Components.NavItem;

public abstract class NavItemBase : ComponentBase
{
    [Parameter] public string Icon { get; set; } = string.Empty;
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public string Href { get; set; } = string.Empty;
    [Parameter] public bool IsActive { get; set; }
    [Parameter] public string? Badge { get; set; }
}