namespace SkillBridge.BuildingBlocks.Domain;

public abstract class AuditableAggregateRoot<TId> : AggregateRoot<TId>, IAuditableEntity, ISoftDeletable
    where TId : notnull
{
    protected AuditableAggregateRoot() { }

    protected AuditableAggregateRoot(TId id) : base(id) { }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
