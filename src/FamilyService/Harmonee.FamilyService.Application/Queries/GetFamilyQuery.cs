using Harmonee.FamilyService.Application.Results;
using Harmonee.Shared.Application.Interfaces;

namespace Harmonee.FamilyService.Application.Queries;

public record GetFamilyQuery(Guid FamilyId) : IQuery<GetFamilyResult>
{
    public string GetRoute() => $"Family/{FamilyId}";
}
