var builder = DistributedApplication.CreateBuilder(args);

var tenantPostgres = builder.AddPostgres("tenant-postgres")
    .WithDataVolume()
    .WithPgAdmin();

var tenantDb = tenantPostgres.AddDatabase("tenant-db");

builder.AddProject<Projects.Tenant_Api>("tenant-api")
    .WithReference(tenantDb)
    .WaitFor(tenantDb);

var administrationPostgres = builder.AddPostgres("administration-postgres")
    .WithDataVolume()
    .WithPgAdmin();

var administrationDb = administrationPostgres.AddDatabase("administration-db");

builder.AddProject<Projects.Administration_Api>("administration-api")
    .WithReference(administrationDb)
    .WaitFor(administrationDb);

builder.Build().Run();