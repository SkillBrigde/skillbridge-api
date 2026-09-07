using SkillBridge.BuildingBlocks.Modules;

namespace SkillBridge.Modules.Catalog;

public sealed class CatalogModule : ModuleDefinition
{
    public override string Name => "Catalog";
    protected override string RoutePrefix => "catalog";
}
