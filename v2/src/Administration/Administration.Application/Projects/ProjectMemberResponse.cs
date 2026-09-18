using System.ComponentModel.DataAnnotations;

namespace Administration.Application.Projects;

public record ProjectMemberResponse(Guid UserId, string Email, string FirstName, string LastName, IReadOnlyList<string> RoleNames);

/// <summary>Adds an existing user to a project by email, optionally assigning them a role in the
/// same call - a role only takes effect once the user also has a membership row, so joining and
/// assigning a first role are one request instead of two.</summary>
public record JoinProjectRequest([Required, EmailAddress] string Email, Guid? RoleId);
