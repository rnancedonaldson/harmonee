using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Interfaces;

public interface ICommandHandler<TCommand> where TCommand : ICommand<IResource, IResult>
{
    Task<IResult> HandleAsync(TCommand command);
}
