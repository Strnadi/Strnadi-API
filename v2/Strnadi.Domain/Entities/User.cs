namespace Strnadi.Domain.Entities;

public partial class User
{
    public int Id { get; set; }

    public string? Email { get; set; }

    public string? Nickname { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? Password { get; set; }

    public DateTime? CreationDate { get; set; }

    public bool? IsEmailVerified { get; set; }

    public bool? Consent { get; set; }

    public string Role { get; set; } = null!;

    public int? PostCode { get; set; }

    public string? City { get; set; }

    public bool Legacy { get; set; }

    public bool Deleted { get; set; }

    public string? Appleid { get; set; }

    public string? GoogleId { get; set; }

    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();

    public virtual ICollection<Photo> Photos { get; set; } = new List<Photo>();

    public virtual ICollection<Recording> Recordings { get; set; } = new List<Recording>();

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
