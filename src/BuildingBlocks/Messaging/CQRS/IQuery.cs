using MediatR;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.BuildingBlocks.Messaging.CQRS;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
