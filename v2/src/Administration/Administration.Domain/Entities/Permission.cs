namespace Administration.Domain.Entities;

public class Permission
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}