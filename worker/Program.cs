using OptimaVerifica.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHostedService<JobProcessorWorker>();

var host = builder.Build();
host.Run();
