using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Models;

public abstract record class Command<TResource, TResult>(object? Payload = null) : Request(Payload), ICommand<TResource, TResult>
     where TResource : IResource where TResult : IResult
{
    public override HttpMethod HttpMethod => HttpMethod.Post;
}
