using Harmonee.FamilyService.Application.Results;
using Harmonee.Shared.Application.Interfaces;

namespace Harmonee.FamilyService.Application.Queries;

public record GetFamilyQuery(Guid FamilyId) : IQuery<GetFamilyResult>
{
    public static string Route = $"Family/{FamilyId}";
    public string GetRoute() => $"Family/{FamilyId}";
}
