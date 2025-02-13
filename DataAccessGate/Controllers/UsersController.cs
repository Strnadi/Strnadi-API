using DataAccessGate.Sql;
using Microsoft.AspNetCore.Mvc;
using Models.Requests;
using LoginRequest = Microsoft.AspNetCore.Identity.Data.LoginRequest;

namespace DataAccessGate.Controllers;

[ApiController]
[Route("/users")]
public class UsersController : ControllerBase
{
    private IConfiguration _configuration;
    private string _connectionString => _configuration["ConnectionStrings:Default"] ?? throw new NullReferenceException("Failed to upload connection string from .env file");
    
    public UsersController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("/users/authorize-user")]
    public IActionResult AuthorizeUser(LoginRequest request)
    { 
        using var repository = new UsersRepository(_connectionString);
        bool authorized = repository.AuthorizeUser(request.Email, request.Password);
        
        return authorized ?
            Accepted() :
            Unauthorized();
    }

    [HttpPost("/users/sign-up")]
    public IActionResult SignUp([FromBody] SignUpRequest request)
    {
        using var repository = new UsersRepository(_connectionString);
        return repository.AddUser(request);
    }
}