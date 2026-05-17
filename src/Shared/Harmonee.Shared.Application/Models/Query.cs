using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Models;

public abstract record class Query<TResource, TResult>() : Request, IQuery<TResource, TResult>
     where TResource : IResource where TResult : IResult
{
}
