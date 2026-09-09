using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities.Enums;

namespace Administration.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    
    public string Name { get; set; }
    
    // https://strnadi.cz
    public string Domain { get; set; }
    
    public ProjectState State { get; set; } 
}

