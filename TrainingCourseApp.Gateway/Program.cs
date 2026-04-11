using System.Text.Json.Nodes;
using Ocelot.DependencyInjection;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Middleware;
using TrainingCourseApp.Gateway.LoadBalancer;

var builder = WebApplication.CreateBuilder(args);

// If environment variable DOWNSTREAM_HOSTS is provided, rebuild DownstreamHostAndPorts in ocelot.json
// Expected format: comma-separated entries: "host:port:weight" or "host:port" (weight defaults to 1)
try
{
    var env = Environment.GetEnvironmentVariable("DOWNSTREAM_HOSTS");
    if (!string.IsNullOrWhiteSpace(env))
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "ocelot.json");
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            var root = JsonNode.Parse(json);
            var routes = root?["Routes"] as JsonArray;
            if (routes != null)
            {
                // parse env into host entries
                var entries = env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var downstreamArray = new JsonArray();
                foreach (var e in entries)
                {
                    // host:port[:weight]
                    var parts = e.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2)
                    {
                        var host = parts[0];
                        if (!int.TryParse(parts[1], out var port))
                            continue;
                        var weight = 1;
                        if (parts.Length >= 3)
                            int.TryParse(parts[2], out weight);

                        var obj = new JsonObject
                        {
                            ["Host"] = host,
                            ["Port"] = port,
                            ["Metadata"] = new JsonObject { ["weight"] = weight.ToString() }
                        };

                        downstreamArray.Add(obj);
                    }
                }

                if (downstreamArray.Count > 0)
                {
                    // Replace DownstreamHostAndPorts for routes that specify WeightedRoundRobin
                    foreach (var r in routes)
                    {
                        var lb = r?["LoadBalancerOptions"] as JsonObject;
                        if (lb != null && string.Equals(lb["Type"]?.ToString(), "WeightedRoundRobin", StringComparison.OrdinalIgnoreCase))
                        {
                            r["DownstreamHostAndPorts"] = downstreamArray;
                        }
                    }

                    // write back file
                    File.WriteAllText(configPath, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }
    }
}
catch
{
    // ignore any errors and fall back to existing config
}

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