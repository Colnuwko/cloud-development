using Amazon.SQS;
using LocalStack.Client.Extensions;
using System.Reflection;
using TrainingCourseApp.FileService;
using TrainingCourseApp.FileService.Messaging;
using TrainingCourseApp.FileService.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var assembly = Assembly.GetExecutingAssembly();
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAwsService<IAmazonSQS>();
builder.Services.AddHostedService<SqsConsumerService>();

builder.AddMinioClient("course-minio");
builder.Services.AddScoped<IS3Service, S3MinioService>();

var app = builder.Build();

app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    var s3 = scope.ServiceProvider.GetRequiredService<IS3Service>();
    await s3.EnsureBucketExists();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
