using System;

namespace Harmonee.Shared.Application.Interfaces;

public interface IQuery<T> where T : IResult
{
    public string GetRoute();
}
