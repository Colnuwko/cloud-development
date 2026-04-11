var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithRedisInsight();

var gateway = builder.AddProject<Projects.TrainingCourseApp_Gateway>("trainingcourseapp-gateway");

var apiPorts = new List<int>();
for (var i = 0; i < 3; i++)
{
    var port = 5500 + i;
    apiPorts.Add(port);

    var service = builder.AddProject<Projects.TrainingCourse_Api>($"trainingcourseapp-api-{i}", launchProfileName: null)
        .WithHttpsEndpoint(port)
        .WithReference(redis)
        .WaitFor(redis);

    gateway.WithReference(service);

    gateway.WaitFor(service);
}

// Соберём значение переменной DOWNSTREAM_HOSTS в формате host:port:weight, разделённое запятыми.
// По умолчанию используем веса 3,2,1 для трёх сервисов (как в ocelot.json). При необходимости можно менять.
try
{
    var entries = new List<string>();
    for (var i = 0; i < apiPorts.Count; i++)
    {
        var host = "localhost";
        var port = apiPorts[i];
        var weight = i == 0 ? 3 : i == 1 ? 2 : 1;
        entries.Add($"{host}:{port}:{weight}");
    }

    var downstreamValue = string.Join(',', entries);
    Environment.SetEnvironmentVariable("DOWNSTREAM_HOSTS", downstreamValue);
}
catch
{
    // ignore
}

builder.AddProject<Projects.Client_Wasm>("client-wasm")
    .WaitFor(gateway);


builder.Build().Run();