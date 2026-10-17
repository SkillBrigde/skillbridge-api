using SkillBridge.Api.Common;
using SkillBridge.Api.Endpoints;
using SkillBridge.Api.Extensions;
using SkillBridge.Api.Modules;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);
var modules = ModuleRegistry.Create();
builder.Services.AddApiServices(builder.Configuration, modules);

var app = builder.Build();
app.UseApiPipeline();
app.MapApiDocumentation();

if (app.Environment.IsDevelopment())
{
    await app.InitializeModulesAsync(modules);
}

app.MapHostEndpoints(modules);
app.MapModuleEndpoints(modules);
app.Run();

public partial class Program;
