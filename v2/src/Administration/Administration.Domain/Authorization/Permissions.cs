namespace Administration.Domain.Authorization;

public static class Permissions
{
    public const string ViewProjects = "projects.view";
    public const string ManageProjects = "projects.manage";
    
    public const string ViewUsersBasic = "users.view.basic";
    public const string ViewUsersConfidential = "users.view.confidential";
    public const string ManageUsers = "users.manage";
    
    public const string ManageRoles = "roles.manage";
    
    public const string ManageArticles = "articles.manage";
    public const string ManageArticleCategories = "article-categories.manage";
    
    public const string ReviewFilteredRecordings = "filtered.review";
    
    public const string ModerateRecordings = "recordings.moderate";
    public const string DeleteRecordings = "recordings.delete";

    public const string SendNotifications = "notifications.send";
}
