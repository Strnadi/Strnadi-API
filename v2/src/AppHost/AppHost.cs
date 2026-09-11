var builder = DistributedApplication.CreateBuilder(args);

var administrationPostgres = builder.AddPostgres("administration-postgres")
    .WithDataVolume()
    .WithPgAdmin();

var administrationDb = administrationPostgres.AddDatabase("Default");

builder.AddProject<Projects.Administration_Api>("administration-api")
    .WithReference(administrationDb)
    .WaitFor(administrationDb);

builder.Build().Run();