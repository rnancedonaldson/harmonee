using System;

namespace Harmonee.Shared.Application.Interfaces;

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult> where TResult : IResult
{
    public Task<TResult> Handle(TQuery query);
}