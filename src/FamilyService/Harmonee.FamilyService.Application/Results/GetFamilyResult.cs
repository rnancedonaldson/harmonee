using Harmonee.FamilyService.Application.Models;
using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Application.Models;

namespace Harmonee.FamilyService.Application.Results;

public record GetFamilyResult(FamilyDto Family) : Result<FamilyDto>
{

}
