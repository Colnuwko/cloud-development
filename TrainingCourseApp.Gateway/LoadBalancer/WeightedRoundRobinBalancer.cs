using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.Values;

namespace TrainingCourseApp.Gateway.LoadBalancer;

/// <summary>
/// Балансировщик нагрузки, реализующий алгоритм Weighted Round Robin
/// </summary>
public class WeightedRoundRobinLoadBalancer : ILoadBalancer
{
    private readonly List<ServiceHostAndPort> _sequence;
    private int _index = -1;
    private readonly object _lock = new();
    private readonly string _type;

    public string Type => _type;

    public WeightedRoundRobinLoadBalancer(List<ServiceHostAndPort> services, int[]? weights = null)
    {
        _type = nameof(WeightedRoundRobinLoadBalancer).Replace("LoadBalancer", "");

        // Веса по умолчанию, если не переданы
        weights ??= new[] { 3, 2, 1};

        _sequence = [];

        for (var i = 0; i < services.Count && i < weights.Length; i++)
        {
            var weight = weights[i];
            for (var j = 0; j < weight; j++)
            {
                _sequence.Add(services[i]);
            }
        }

        // Если сервисов больше чем весов, оставшимся даем вес 1
        for (var i = weights.Length; i < services.Count; i++)
        {
            _sequence.Add(services[i]);
        }
    }

    public Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext context)
    {
        lock (_lock)
        {
            if (_sequence.Count == 0)
            {
                throw new InvalidOperationException("No available downstream services.");
            }

            _index = (_index + 1) % _sequence.Count;
            return Task.FromResult<Response<ServiceHostAndPort>>(
                new OkResponse<ServiceHostAndPort>(_sequence[_index]));
        }
    }

    public void Release(ServiceHostAndPort hostAndPort)
    {
    }
}