
using SkillBridge.BuildingBlocks.Contracts;

namespace SkillBridge.Modules.Booking;

public sealed class BookingModule : ModuleDefinition
{
    public override string Name => "Booking";
    public override string RoutePrefix => "booking";
}
