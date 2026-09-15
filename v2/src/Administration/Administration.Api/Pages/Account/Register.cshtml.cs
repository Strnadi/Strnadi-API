using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Administration.Api.Resources;
using Administration.Domain.Entities;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Administration.Api.Pages.Account;

public class RegisterModel(
    UserManager<User> users,
    SignInManager<User> signIn,
    IEmailSender<User> emailSender,
    AdminDbContext db,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Currently active, platform-wide documents shown as consent checkboxes (some required, some optional).</summary>
    public List<Document> ConsentDocuments { get; set; } = [];

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "EmailInvalid")]
        public string Email { get; set; } = string.Empty;

        public string? UserName { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string LastName { get; set; } = string.Empty;

        public string? City { get; set; }

        public int? PostCode { get; set; }

        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "PasswordsDoNotMatch")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public List<Guid> AcceptedDocumentIds { get; set; } = [];
    }

    public async Task OnGetAsync()
    {
        ConsentDocuments = await LoadConsentDocumentsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ConsentDocuments = await LoadConsentDocumentsAsync();

        if (ConsentDocuments.Any(document => document.IsRequired && !Input.AcceptedDocumentIds.Contains(document.Id)))
            ModelState.AddModelError(string.Empty, localizer["MustAcceptAllDocuments"]);

        if (!ModelState.IsValid)
            return Page();

        var user = new User
        {
            UserName = string.IsNullOrWhiteSpace(Input.UserName) ? null : Input.UserName,
            Email = Input.Email,
            FirstName = Input.FirstName,
            LastName = Input.LastName,
            City = string.IsNullOrWhiteSpace(Input.City) ? null : Input.City,
            PostCode = Input.PostCode,
            CreatedAt = DateTime.UtcNow,
            PreferredLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
        };

        var result = await users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        var acceptedAt = DateTime.UtcNow;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        foreach (var document in ConsentDocuments.Where(document => Input.AcceptedDocumentIds.Contains(document.Id)))
        {
            db.DocumentAcceptances.Add(new DocumentAcceptance
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DocumentId = document.Id,
                AcceptedAt = acceptedAt,
                IpAddress = ipAddress
            });
        }

        await db.SaveChangesAsync();

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var confirmLink = $"{Request.Scheme}://{Request.Host}/account/confirm-email" +
            $"?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmLink);

        await signIn.SignInAsync(user, isPersistent: true);
        return LocalRedirect(string.IsNullOrEmpty(ReturnUrl) ? "/dashboard" : ReturnUrl);
    }

    private Task<List<Document>> LoadConsentDocumentsAsync() =>
        db.Documents
            .Where(document => document.IsActive && document.ProjectId == null)
            .OrderByDescending(document => document.IsRequired)
            .ThenBy(document => document.Type)
            .ToListAsync();
}
