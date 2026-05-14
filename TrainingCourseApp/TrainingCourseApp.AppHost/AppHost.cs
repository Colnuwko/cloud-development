using Amazon;
using Aspire.Hosting.LocalStack.Container;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithRedisInsight();

var awsConfig = builder.AddAWSSDKConfig()
    .WithProfile("default")
    .WithRegion(RegionEndpoint.EUCentral1);

var localstack = builder
    .AddLocalStack("course-localstack", awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
        container.Port = 4566;
        container.AdditionalEnvironmentVariables.Add("DEBUG", "1");
    });

var awsResources = builder
    .AddAWSCloudFormationTemplate("resources", "CloudFormation/course-template-sqs.yaml", "course")
    .WithReference(awsConfig);

var minio = builder.AddMinioContainer("course-minio");

var gateway = builder.AddProject<Projects.TrainingCourseApp_Gateway>("trainingcourseapp-gateway");

var apiPorts = new List<int>();
for (var i = 0; i < 3; i++)
{
    var port = 5500 + i;
    apiPorts.Add(port);

    var service = builder.AddProject<Projects.TrainingCourse_Api>($"trainingcourseapp-api-{i}", launchProfileName: null)
        .WithHttpsEndpoint(port)
        .WithReference(redis)
        .WithReference(awsResources)
        .WithEnvironment("Settings__MessageBroker", "SQS")
        .WaitFor(redis)
        .WaitFor(awsResources);

    gateway.WithReference(service);
    gateway.WaitFor(service);
}

try
{
    var entries = new List<string>();
    for (var i = 0; i < apiPorts.Count; i++)
    {
        var weight = i == 0 ? 3 : i == 1 ? 2 : 1;
        entries.Add($"localhost:{apiPorts[i]}:{weight}");
    }
    Environment.SetEnvironmentVariable("DOWNSTREAM_HOSTS", string.Join(',', entries));
}
catch
{
    // ignore
}

var fileService = builder.AddProject<Projects.TrainingCourseApp_FileService>("trainingcourseapp-fileservice")
    .WithReference(awsResources)
    .WithReference(minio)
    .WithEnvironment("Settings__MessageBroker", "SQS")
    .WithEnvironment("Settings__S3Hosting", "Minio")
    .WithEnvironment("AWS__Resources__MinioBucketName", "course-bucket")
    .WaitFor(awsResources)
    .WaitFor(minio);

builder.AddProject<Projects.Client_Wasm>("client-wasm")
    .WaitFor(gateway);

builder.UseLocalStack(localstack);

builder.Build().Run();
