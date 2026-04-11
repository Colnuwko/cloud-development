using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.Values;

namespace TrainingCourseApp.Gateway.LoadBalancer;

/// <summary>
/// Балансировщик нагрузки, реализующий алгоритм Weighted Round Robin
/// </summary>
public class WeightedRoundRobinLoadBalancer : ILoadBalancer
{
    private readonly List<ServiceHostAndPort> _services;
    private readonly int[] _weights;          // исходные веса
    private readonly int[] _currentWeights;   // текущие веса для
    private readonly int _totalWeight;        // сумма всех весов
    private readonly object _lock = new();

    public string Type => nameof(WeightedRoundRobinLoadBalancer).Replace("LoadBalancer", "");

    public WeightedRoundRobinLoadBalancer(List<ServiceHostAndPort> services, int[]? weights = null)
    {

        if (services == null || services.Count == 0)
            throw new ArgumentException("Services list cannot be null or empty.", nameof(services));

        _services = new List<ServiceHostAndPort>(services);
        _weights = new int[_services.Count];

        // Инициализация весов
        if (weights != null)
        {
            for (var i = 0; i < _services.Count && i < weights.Length; i++)
                _weights[i] = weights[i];
            for (var i = weights.Length; i < _services.Count; i++)
                _weights[i] = 1;
        }
        else
        {
            for (var i = 0; i < _services.Count; i++)
            {
                _weights[i] = i switch
                {
                    0 => 3,
                    1 => 2,
                    2 => 1,
                    _ => 1
                };
            }
        }
        _totalWeight = _weights.Sum();
        if (_totalWeight <= 0)
            throw new InvalidOperationException("Total weight must be greater than zero.");

        _currentWeights = new int[_services.Count];
        Array.Copy(_weights, _currentWeights, _services.Count);
    }

    public Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext context)
    {
        lock (_lock)
        {
            var maxIndex = 0;
            var maxWeight = _currentWeights[0];
            for (var i = 1; i < _services.Count; i++)
            {
                if (_currentWeights[i] > maxWeight)
                {
                    maxWeight = _currentWeights[i];
                    maxIndex = i;
                }
            }
            _currentWeights[maxIndex] -= _totalWeight;

            for (var i = 0; i < _services.Count; i++)
            {
                _currentWeights[i] += _weights[i];
            }

            return Task.FromResult<Response<ServiceHostAndPort>>(
                new OkResponse<ServiceHostAndPort>(_services[maxIndex]));
        }
    }

    public void Release(ServiceHostAndPort hostAndPort)
    {
    
    }
}