using TrainingCourse.Api.Services;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");

builder.Services.AddScoped<ICourseService, CourseService>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/api/courses", async (int id, ICourseService patientService) =>
{
    var patient = await patientService.GetCourse(id);
    return Results.Ok(patient);
});

app.Run();