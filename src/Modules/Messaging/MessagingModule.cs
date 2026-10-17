
using SkillBridge.BuildingBlocks.Contracts;

namespace SkillBridge.Modules.Messaging;

public sealed class MessagingModule : ModuleDefinition
{
    public override string Name => "Messaging";
    public override string RoutePrefix => "messaging";
}
