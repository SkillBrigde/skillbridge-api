using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillBridge.BuildingBlocks.Modules;
using SkillBridge.Modules.Booking.Application;
using SkillBridge.Modules.Booking.Endpoints;
using SkillBridge.Modules.Booking.Infrastructure;

namespace SkillBridge.Modules.Booking;

public sealed class BookingModule : ModuleDefinition
{
    public override string Name => "Booking";
    public override string RoutePrefix => "booking";

    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContext<BookingDbContext>(options => options.UseNpgsql(connection,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BookingDbContext.Schema)));
        services.TryAddScoped<IBookingServiceDirectory, UnavailableBookingDependencies>();
        services.TryAddScoped<IBookingSlotReservations, UnavailableBookingDependencies>();
        services.TryAddScoped<IBookingCompletionEvidence, UnavailableBookingDependencies>();
        services.AddScoped<Bookings>();
        services.AddScoped<Reschedules>();
        services.AddHostedService<BookingMaintenance>();
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.MapEndpoints(endpoints);
        endpoints.MapBookingEndpoints();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BookingDbContext>().Database.MigrateAsync();
    }
}
