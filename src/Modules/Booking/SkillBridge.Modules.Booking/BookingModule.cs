using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Booking;

public sealed class BookingModule : ModuleDefinition
{
    public override string Name => "Booking";
    protected override string RoutePrefix => "bookings";
}
