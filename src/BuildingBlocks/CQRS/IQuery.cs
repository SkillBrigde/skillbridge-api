using MediatR;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.BuildingBlocks.CQRS;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
