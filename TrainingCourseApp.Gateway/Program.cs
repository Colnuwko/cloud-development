using Ocelot.DependencyInjection;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Middleware;
using TrainingCourseApp.Gateway.LoadBalancer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddServiceDiscovery();
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddCors(options =>
    options.AddPolicy("AllowClient", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [])
              .WithMethods("GET")
              .AllowAnyHeader()));

builder.Services.AddOcelot();
builder.Services.AddSingleton<ILoadBalancerCreator, WeightedRoundRobinCreator>();

var app = builder.Build();

app.UseCors("AllowClient");
app.MapDefaultEndpoints();

await app.UseOcelot();

app.Run();