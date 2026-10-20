using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillBridge.BuildingBlocks.Modules;
using SkillBridge.Modules.Payments.Application;
using SkillBridge.Modules.Payments.Endpoints;
using SkillBridge.Modules.Payments.Infrastructure;

namespace SkillBridge.Modules.Payments;

public sealed class PaymentsModule : ModuleDefinition
{
    public override string Name => "Payments";
    public override string RoutePrefix => "payments";
    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(connection,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PaymentsDbContext.Schema)));
        services.TryAddScoped<IPaymentBookings, UnavailablePaymentBookings>();
        services.AddHttpClient<IPaymentGateway, PaymentGateway>(client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<PaymentsService>(); services.AddHostedService<PaymentReconciliationWorker>();
    }
    public override void MapEndpoints(IEndpointRouteBuilder endpoints) { base.MapEndpoints(endpoints); endpoints.MapPaymentEndpoints(); }
    public override async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
    }
}
