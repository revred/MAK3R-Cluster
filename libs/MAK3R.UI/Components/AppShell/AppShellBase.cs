using Microsoft.AspNetCore.Components;

namespace MAK3R.UI.Components.AppShell;

public abstract class AppShellBase : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Environment { get; set; } = "DEV";
    [Parameter] public bool IsAuthenticated { get; set; }
    [Parameter] public string? UserName { get; set; }
    [Parameter] public string? UserRole { get; set; }
    [Parameter] public bool IsOnline { get; set; } = true;
    [Parameter] public int PendingSyncCount { get; set; }
    [Parameter] public List<BreadcrumbItem>? Breadcrumbs { get; set; }

    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected List<NavItemModel> NavigationItems { get; set; } = new()
    {
        new("dashboard", "Dashboard", "/", null),
        new("settings_input_composite", "Onboard", "/onboard", null),
        new("business", "Companies", "/companies", null),
        new("account_tree", "Twin", "/twin", null),
        new("storefront", "Shopfront", "/shopfront", null),
        new("hub", "Connectors", "/connectors", null),
        new("warning", "Anomalies", "/anomalies", "3"),
        new("precision_manufacturing", "Machines", "/machines", null),
    };

    protected bool IsActive(string href)
    {
        var currentUri = Navigation.ToBaseRelativePath(Navigation.Uri);
        if (href == "/")
            return currentUri == "";
        return currentUri.StartsWith(href.TrimStart('/'), StringComparison.OrdinalIgnoreCase);
    }

    protected string GetUserInitials()
    {
        if (string.IsNullOrWhiteSpace(UserName))
            return "?";

        var parts = UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        
        return UserName[0].ToString().ToUpper();
    }
}

public record NavItemModel(
    string Icon,
    string Label,
    string Href,
    string? Badge
);

public record BreadcrumbItem(
    string Label,
    string Href
);