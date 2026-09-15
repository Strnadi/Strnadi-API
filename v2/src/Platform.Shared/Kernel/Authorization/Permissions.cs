namespace Platform.Shared.Kernel.Authorization;

public static class Permissions
{
    public const string ViewProjects = "projects.view";
    public const string ManageProjects = "projects.manage";
    
    public const string ViewUsersBasic = "users.view.basic";
    public const string ViewUsersConfidential = "users.view.confidential";
    public const string ManageUsers = "users.manage";
    
    public const string ManageRoles = "roles.manage";

    public const string ManageDevices = "devices.manage";
    
    public const string ManageArticles = "articles.manage";
    public const string ManageArticleCategories = "articles.categories.manage";
    public const string ManageArticleTranslations = "articles.translations.manage";
    
    public const string ReviewFilteredRecordings = "filtered.review";
    
    public const string ModerateRecordings = "recordings.moderate";
    public const string DeleteRecordings = "recordings.delete";

    public const string SendNotifications = "notifications.send";

    public const string ManageAchievements = "achievements.manage";

    public const string ManageDocuments = "documents.manage";
}
