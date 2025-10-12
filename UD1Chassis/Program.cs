using Microsoft.Extensions.Options;
using UD1Chassis;
using UD1Chassis.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddOptions<PorcupineOptions>()
    .Bind(builder.Configuration.GetSection(nameof(PorcupineOptions)))
    .ValidateOnStart();

var host = builder.Build();
host.Run();
