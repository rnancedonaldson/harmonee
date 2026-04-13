using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Interfaces;

public interface IQuery<TResource, TResult> where TResource : IResource where TResult : IResult
{
    
}
