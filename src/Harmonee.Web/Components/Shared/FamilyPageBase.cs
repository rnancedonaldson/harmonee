using Harmonee.Api.Contracts.Families;
using Harmonee.Web.Api;
using Harmonee.Web.Services;
using Microsoft.AspNetCore.Components;

namespace Harmonee.Web.Components.Shared;

/// <summary>Base for pages under /families/{FamilyId}: loads the family (and the caller's access) once per request.</summary>
public abstract class FamilyPageBase : ComponentBase
{
    [Parameter]
    public Guid FamilyId { get; set; }

    [CascadingParameter]
    protected HttpContext? HttpContext { get; set; }

    [Inject]
    protected HarmoneeApiClient Api { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [Inject]
    protected UserTimeZone TimeZone { get; set; } = default!;

    [Inject]
    protected TimeProvider Clock { get; set; } = default!;

    protected FamilyResponse? Family { get; private set; }

    protected ApiProblem? LoadProblem { get; set; }

    /// <summary>Error from the last form submission, shown above the form.</summary>
    protected ApiProblem? ActionProblem { get; set; }

    protected CancellationToken Aborted => HttpContext?.RequestAborted ?? CancellationToken.None;

    protected override async Task OnInitializedAsync()
    {
        var family = await Api.GetFamilyAsync(FamilyId, Aborted);
        if (!family.IsSuccess)
        {
            LoadProblem = family.Problem;
            return;
        }

        Family = family.Value;
        await LoadAsync();
    }

    /// <summary>Loads page-specific data after the family has loaded.</summary>
    protected virtual Task LoadAsync() => Task.CompletedTask;

    protected string FamilyUrl(string suffix = "") => $"families/{FamilyId}{suffix}";

    /// <summary>
    /// Handles a mutation's outcome. On success, redirects (Post/Redirect/Get) so refreshing doesn't resubmit;
    /// on failure, keeps the user on the page with the problem shown.
    /// </summary>
    protected void Complete(ApiProblem? problem, string? redirectTo = null)
    {
        if (problem is null)
        {
            Navigation.NavigateTo(redirectTo ?? Navigation.Uri);
            return;
        }

        ActionProblem = problem.IsConcurrencyConflict
            ? problem with { Title = "Someone else changed this while you were editing. The latest version is shown below — please try again.", Errors = new Dictionary<string, string[]>(StringComparer.Ordinal) }
            : problem;
    }
}
