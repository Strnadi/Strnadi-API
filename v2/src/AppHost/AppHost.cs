var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.Tenant_Api>("tenant-api");
builder.Build().Run();