using MediatR;
using SkillBridge.BuildingBlocks.Results;

namespace SkillBridge.BuildingBlocks.CQRS;

public interface ICommand : IRequest<Result>;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;
