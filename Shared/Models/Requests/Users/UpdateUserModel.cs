using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.Requests.Users;

public class UpdateUserModel
{
    [Column("nickname")]
    public string? Nickname { get; set; }

    [Column("first_name")]
    public string? FirstName { get; set; }

    [Column("last_name")]
    public string? LastName { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("post_code")]
    public string? PostCode { get; set; }
}