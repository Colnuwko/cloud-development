using Ocelot.Configuration;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.ServiceDiscovery.Providers;
using Ocelot.Values;
using TrainingCourseApp.Gateway.LoadBalancer;
/// <summary>
/// Создатель балансировщика нагрузки WeightedRoundRobin с поддержкой весов из конфигурации
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

        var balancer = new WeightedRoundRobinLoadBalancer(hostAndPorts);

        return new OkResponse<ILoadBalancer>(balancer);
    }
}