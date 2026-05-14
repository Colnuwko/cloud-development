using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using TrainingCourse.Api.Models;
using Xunit.Abstractions;

namespace TrainingCourseApp.AppHost.Tests;

/// <summary>
/// Интеграционные тесты для проверки микросервисного пайплайна:
/// API генерации курсов → SQS (LocalStack) → файловый сервис → MinIO.
/// </summary>
/// <param name="output">Служба журналирования юнит-тестов</param>
public class IntegrationTest(ITestOutputHelper output) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private DistributedApplication? _app;
    private HttpClient? _gatewayClient;
    private HttpClient? _sinkClient;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TrainingCourseApp_AppHost>(cancellationToken);
        builder.Configuration["DcpPublisher:RandomizePorts"] = "false";
        builder.Services.AddLogging(logging =>
        {
            logging.AddXUnit(output);
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Aspire.Hosting.Dcp", LogLevel.Debug);
            logging.AddFilter("Aspire.Hosting", LogLevel.Debug);
        });

        _app = await builder.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);
        _gatewayClient = _app.CreateHttpClient("trainingcourseapp-gateway", "http");
        _sinkClient = _app.CreateHttpClient("trainingcourseapp-fileservice", "http");
    }

    /// <summary>
    /// Проверяет, что вызов гейтвея:
    /// <list type="bullet">
    /// <item><description>В ответ отправляет сгенерированный курс</description></item>
    /// <item><description>Сериализует курс в S3-хранилище</description></item>
    /// <item><description>Данные из предыдущих пунктов идентичны</description></item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task TestPipeline()
    {
        var random = new Random();
        var id = random.Next(1, 100);
        using var gatewayResponse = await _gatewayClient!.GetAsync($"/courses?id={id}");
        var apiCourse = JsonSerializer.Deserialize<Course>(
            await gatewayResponse.Content.ReadAsStringAsync(), _jsonOptions);

        await Task.Delay(5000);
        using var listResponse = await _sinkClient!.GetAsync($"/api/s3");
        var courseList = JsonSerializer.Deserialize<List<string>>(
            await listResponse.Content.ReadAsStringAsync(), _jsonOptions);
        using var s3Response = await _sinkClient!.GetAsync($"/api/s3/course_{id}.json");
        var s3Course = JsonSerializer.Deserialize<Course>(
            await s3Response.Content.ReadAsStringAsync(), _jsonOptions);

        Assert.NotNull(courseList);
        Assert.Single(courseList);
        Assert.NotNull(apiCourse);
        Assert.NotNull(s3Course);
        Assert.Equal(id, s3Course.Id);
        Assert.Equivalent(apiCourse, s3Course);
    }

    /// <summary>
    /// Проверяет идемпотентность по идентификатору курса:
    /// повторный запрос того же id обслуживается из Redis-кэша,
    /// продюсер повторно не публикует сообщение, и в бакете остаётся
    /// ровно один объект <c>course_{id}.json</c>.
    /// </summary>
    [Fact]
    public async Task RepeatedRequestDoesNotDuplicateObjectInBucket()
    {
        var id = new Random().Next(1, 100);

        using var firstResponse = await _gatewayClient!.GetAsync($"/courses?id={id}");
        var firstCourse = JsonSerializer.Deserialize<Course>(
            await firstResponse.Content.ReadAsStringAsync(), _jsonOptions);

        using var secondResponse = await _gatewayClient!.GetAsync($"/courses?id={id}");
        var secondCourse = JsonSerializer.Deserialize<Course>(
            await secondResponse.Content.ReadAsStringAsync(), _jsonOptions);

        await Task.Delay(5000);

        using var listResponse = await _sinkClient!.GetAsync("/api/s3");
        var courseList = JsonSerializer.Deserialize<List<string>>(
            await listResponse.Content.ReadAsStringAsync(), _jsonOptions);

        Assert.NotNull(firstCourse);
        Assert.NotNull(secondCourse);
        Assert.Equivalent(firstCourse, secondCourse);
        Assert.NotNull(courseList);
        Assert.Single(courseList);
        Assert.Equal($"course_{id}.json", courseList[0]);
    }

    /// <summary>
    /// Проверяет, что пайплайн корректно обрабатывает несколько разных id
    /// в рамках одного запуска: каждый сгенерированный курс попадает в бакет
    /// под собственным ключом, и содержимое каждого файла совпадает с ответом API.
    /// </summary>
    [Fact]
    public async Task MultipleCoursesArePersistedIndependently()
    {
        var ids = Enumerable.Range(0, 3).Select(i => 200 + i).ToArray();

        var apiCourses = new Dictionary<int, Course>();
        foreach (var id in ids)
        {
            using var response = await _gatewayClient!.GetAsync($"/courses?id={id}");
            var course = JsonSerializer.Deserialize<Course>(
                await response.Content.ReadAsStringAsync(), _jsonOptions);
            Assert.NotNull(course);
            apiCourses[id] = course!;
        }

        await Task.Delay(5000);

        using var listResponse = await _sinkClient!.GetAsync("/api/s3");
        var courseList = JsonSerializer.Deserialize<List<string>>(
            await listResponse.Content.ReadAsStringAsync(), _jsonOptions);

        Assert.NotNull(courseList);
        Assert.Equal(ids.Length, courseList.Count);
        foreach (var id in ids)
        {
            Assert.Contains($"course_{id}.json", courseList);

            using var s3Response = await _sinkClient!.GetAsync($"/api/s3/course_{id}.json");
            var s3Course = JsonSerializer.Deserialize<Course>(
                await s3Response.Content.ReadAsStringAsync(), _jsonOptions);

            Assert.NotNull(s3Course);
            Assert.Equivalent(apiCourses[id], s3Course);
        }
    }

    /// <summary>
    /// Проверяет, что после round-trip через брокер и объектное хранилище
    /// сохранены инварианты доменной модели: рейтинг в диапазоне 1–5,
    /// текущее число студентов не превышает максимум, дата окончания не раньше
    /// даты начала, обязательные строковые поля не пусты.
    /// </summary>
    [Fact]
    public async Task PersistedCourseSatisfiesDomainInvariants()
    {
        var id = new Random().Next(300, 400);
        using var apiResponse = await _gatewayClient!.GetAsync($"/courses?id={id}");
        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);

        await Task.Delay(5000);

        using var s3Response = await _sinkClient!.GetAsync($"/api/s3/course_{id}.json");
        var s3Course = JsonSerializer.Deserialize<Course>(
            await s3Response.Content.ReadAsStringAsync(), _jsonOptions);

        Assert.NotNull(s3Course);
        Assert.Equal(id, s3Course!.Id);
        Assert.False(string.IsNullOrWhiteSpace(s3Course.CourseName));
        Assert.False(string.IsNullOrWhiteSpace(s3Course.TeacherFullName));
        Assert.InRange(s3Course.Rating, 1, 5);
        Assert.InRange(s3Course.CurrentStudents, 0, s3Course.MaxStudents);
        Assert.True(s3Course.EndDate >= s3Course.StartDate);
        Assert.True(s3Course.Price > 0);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        _gatewayClient?.Dispose();
        _sinkClient?.Dispose();
        await _app!.StopAsync();
        await _app.DisposeAsync();
    }
}
