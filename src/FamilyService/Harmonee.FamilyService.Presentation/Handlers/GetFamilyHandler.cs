using System;
using Harmonee.FamilyService.Application.Queries;
using Harmonee.FamilyService.Application.Results;
using Harmonee.Shared.Application.Interfaces;

namespace Harmonee.FamilyService.Presentation.Handlers;

public class GetFamilyHandler : IQueryHandler<GetFamilyQuery, GetFamilyResult>
{
    public Task<GetFamilyResult> Handle(GetFamilyQuery query)
    {
        throw new NotImplementedException();
    }
}
