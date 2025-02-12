using DataAccessGate.Sql;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace DataAccessGate.Controllers;

[ApiController]
[Route("/users")]
internal class UsersController : ControllerBase
{
    private IConfiguration _configuration;
    private string _connectionString => _configuration["DB_CONNECTION_STRING"] ?? throw new NullReferenceException("Failed to upload connection string from .env file");
    
    public UsersController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("/users/authorize-user")]
    public IActionResult AuthorizeUser(LoginRequest request)
    {
        var repository = new UsersRepository(_connectionString);
        bool authorized = repository.AuthorizeUser(request.Email, request.Password);
        
        return authorized ?
            Ok() :
            Unauthorized();
    }
}