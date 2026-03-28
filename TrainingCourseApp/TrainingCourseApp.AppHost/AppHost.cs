var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithRedisInsight();

var gateway = builder.AddProject<Projects.TrainingCourseApp_Gateway>("trainingcourseapp-gateway");

for (var i = 0; i < 3; i++)
{
    var service = builder.AddProject<Projects.TrainingCourse_Api>($"trainingcourseapp-api-{i}", launchProfileName: null)
        .WithHttpsEndpoint(8000 + i)
        .WithReference(redis)
        .WaitFor(redis);
    gateway.WaitFor(service);
}

builder.AddProject<Projects.Client_Wasm>("client-wasm")
    .WaitFor(gateway);


builder.Build().Run();