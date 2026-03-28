using TrainingCourse.Api.Services;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");

builder.Services.AddScoped<ICourseService, CourseService>();
//builder.Services.AddCors(options =>
//    options.AddPolicy("AllowClient", policy =>
//        policy.WithOrigins(builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [])
//              .WithMethods("GET")
//              .AllowAnyHeader()));

var app = builder.Build();

//app.UseCors("AllowClient");
app.MapDefaultEndpoints();

app.MapGet("/api/courses", async (int id, ICourseService patientService) =>
{
    var patient = await patientService.GetCourse(id);
    return Results.Ok(patient);
});

app.Run();