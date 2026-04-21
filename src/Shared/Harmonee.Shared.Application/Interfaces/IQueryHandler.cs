using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Interfaces;

public interface IQueryHandler<TQuery> where TQuery : IQuery<IResource, IResult>
{
    public Task<IResult> HandleAsync(TQuery query);
}