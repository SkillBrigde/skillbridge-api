using MediatR;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.BuildingBlocks.CQRS;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
