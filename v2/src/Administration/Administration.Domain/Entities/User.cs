namespace Administration.Domain.Entities;

public class User
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public bool IsEmailConfirmed { get; set; }

    // Nullable: accounts that only ever sign in through Google/Apple have no local password.
    public string? PasswordHash { get; set; }

    public string? GoogleId { get; set; }

    public string? AppleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool Deleted { get; set; }

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}