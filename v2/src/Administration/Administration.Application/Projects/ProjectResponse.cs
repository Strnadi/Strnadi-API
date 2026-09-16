namespace Administration.Application.Projects;

public record ProjectResponse(Guid Id, string Name, string? Description, string Domain);
