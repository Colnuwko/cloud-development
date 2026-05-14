using Amazon.SQS;
using LocalStack.Client.Extensions;
using TrainingCourse.Api.Messaging;
using TrainingCourse.Api.Services;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddScoped<IProducerService, SqsProducerService>();
builder.Services.AddAwsService<IAmazonSQS>();

builder.Services.AddScoped<ICourseService, CourseService>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/api/courses", async (int id, ICourseService courseService) =>
{
    var course = await courseService.GetCourse(id);
    return Results.Ok(course);
});

app.Run();
