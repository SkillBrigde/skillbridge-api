using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.BuildingBlocks.Events;
using SkillBridge.Modules.Catalog.Domain;

namespace SkillBridge.Modules.Catalog.Infrastructure;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Type).HasMaxLength(500);
            builder.Property(message => message.Content).HasColumnType("jsonb");
            builder.HasIndex(message => message.OccurredOnUtc).HasFilter("processed_on_utc IS NULL");
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));

        modelBuilder.Entity<Category>().Property(category => category.SortOrder).HasColumnName("display_order");
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", table =>
        {
            table.HasCheckConstraint("CK_categories_parent", "parent_id IS NULL OR parent_id <> id");
            table.HasCheckConstraint("CK_categories_display_order", "display_order >= 0");
        });
        builder.HasKey(category => category.Id);
        builder.Ignore(category => category.DomainEvents);
        builder.Property(category => category.Name).HasMaxLength(150).IsRequired();
        builder.Property(category => category.Slug).HasMaxLength(150).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(4000);
        builder.Property(category => category.IconUrl).HasMaxLength(2048);
        builder.HasIndex(category => category.Slug).IsUnique();
        builder.HasOne<Category>().WithMany().HasForeignKey(category => category.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(category => !category.IsDeleted);
    }
}

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");
        builder.HasKey(skill => skill.Id);
        builder.Ignore(skill => skill.DomainEvents);
        builder.Property(skill => skill.Name).HasMaxLength(150).IsRequired();
        builder.Property(skill => skill.Slug).HasMaxLength(150).IsRequired();
        builder.Property(skill => skill.Description).HasMaxLength(4000);
        builder.HasIndex(skill => skill.Slug).IsUnique();
        builder.HasOne<Category>().WithMany().HasForeignKey(skill => skill.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(skill => skill.Tags).WithMany().UsingEntity<Dictionary<string, object>>(
            "skill_tags",
            tags => tags.HasOne<Tag>().WithMany().HasForeignKey("TagId").OnDelete(DeleteBehavior.Cascade),
            skills => skills.HasOne<Skill>().WithMany().HasForeignKey("SkillId").OnDelete(DeleteBehavior.Cascade),
            join => join.HasKey("SkillId", "TagId"));
        builder.Navigation(skill => skill.Tags).HasField("_tags").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");
        builder.HasKey(tag => tag.Id);
        builder.Ignore(tag => tag.DomainEvents);
        builder.Property(tag => tag.Name).HasMaxLength(150).IsRequired();
        builder.Property(tag => tag.Slug).HasMaxLength(150).IsRequired();
        builder.HasIndex(tag => tag.Slug).IsUnique();
    }
}
