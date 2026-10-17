using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Behaviors;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.BuildingBlocks.Infrastructure.Interceptors;

namespace SkillBridge.BuildingBlocks.Extensions;

public static class BuildingBlocksExtensions
{
    public static IServiceCollection AddBuildingBlocks(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] moduleAssemblies)
    {
        var targetAssemblies = moduleAssemblies.Length > 0
            ? moduleAssemblies
            : [typeof(BuildingBlocksExtensions).Assembly];

        // 1. TimeProvider chuẩn hóa thời gian cho hệ thống
        services.AddSingleton(TimeProvider.System);

        // 2. MediatR với các Pipeline Behaviors (Logging, Validation)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(targetAssemblies);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // 3. FluentValidation quét toàn bộ validator trong các module assemblies
        if (targetAssemblies.Length > 0)
        {
            services.AddValidatorsFromAssemblies(targetAssemblies, includeInternalTypes: true);
        }

        // 4. Event Bus cho giao tiếp phi đồng bộ liên module
        services.AddScoped<IEventBus, InMemoryEventBus>();

        // 5. EF Core Interceptors tự động cập nhật Audit và Domain Events
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        return services;
    }
}
