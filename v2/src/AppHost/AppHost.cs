var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var db = postgres.AddDatabase("Default");

builder.AddProject<Projects.Tenant_Api>("tenant-api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();