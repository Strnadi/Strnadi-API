using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Shared.Models.Database.Achievements;

public class Achievement
{
    public int Id { get; set; }
    
    public string Title { get; set; }
    
    public string Description { get; set; }
    
    public string Sql { get; set; }
    
    [JsonIgnore]
    public string ImagePath { get; set; }
    
    [NotMapped]
    public string ImageUrl { get; set; }
}