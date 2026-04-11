
using System.Text.Json;
using Ocelot.Configuration;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.ServiceDiscovery.Providers;

namespace TrainingCourseApp.Gateway.LoadBalancer;

/// <summary>
/// Создатель балансировщика нагрузки Weighted Round Robin с поддержкой весов из конфигурации
/// </summary>
public class WeightedRoundRobinCreator : ILoadBalancerCreator
{
    public string Type => nameof(WeightedRoundRobinCreator).Replace("Creator", "");
    public Response<ILoadBalancer> Create(
        DownstreamRoute route,
        IServiceDiscoveryProvider serviceProvider)
    {
        var services = serviceProvider.GetAsync().Result;

        var hostAndPorts = services
            .Select(s => s.HostAndPort)
            .ToList();

        // Упрощённое чтение весов из ocelot.json (ожидается стандартная структура)
        var weights = new List<int>();
        try
        {
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "ocelot.json");
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);

            var routes = doc.RootElement.GetProperty("Routes");

            foreach (var r in routes.EnumerateArray())
            {
                if (r.TryGetProperty("LoadBalancerOptions", out var lb) &&
                    lb.TryGetProperty("Type", out var t) &&
                    string.Equals(t.GetString(), "WeightedRoundRobin", StringComparison.OrdinalIgnoreCase))
                {
                    var dhps = r.GetProperty("DownstreamHostAndPorts");
                    foreach (var hp in dhps.EnumerateArray())
                    {
                        var w = 1;
                        if (hp.TryGetProperty("Metadata", out var meta) && meta.TryGetProperty("weight", out var wEl))
                        {
                            if (wEl.ValueKind == JsonValueKind.String)
                                int.TryParse(wEl.GetString(), out w);
                            else if (wEl.ValueKind == JsonValueKind.Number)
                                wEl.TryGetInt32(out w);
                        }

                        if (w <= 0) w = 1;
                        weights.Add(w);
                    }

                    break; // нашли нужный маршрут — выходим
                }
            }
        }
        catch
        {
            // при любой ошибке используем веса по умолчанию
        }

        if (weights.Count == 0)
            weights = Enumerable.Repeat(1, hostAndPorts.Count).ToList();

        var balancer = new WeightedRoundRobinLoadBalancer(hostAndPorts, weights.ToArray());

        return new OkResponse<ILoadBalancer>(balancer);
    }
}