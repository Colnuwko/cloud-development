using System.Net;
using System.Text.Json;
using Amazon.SQS;
using TrainingCourse.Api.Models;

namespace TrainingCourse.Api.Messaging;

/// <summary>
/// Реализация продюсера поверх AWS SQS. Используется как сервисом генерации,
/// так и через LocalStack — имя/URL очереди берётся из конфигурации
/// (ключ <c>AWS:Resources:SQSQueueName</c>), которую заполняет CloudFormation-стек AppHost.
/// </summary>
/// <param name="client">Клиент AWS SQS, сконфигурированный через LocalStack</param>
/// <param name="configuration">Конфигурация приложения, содержащая адрес очереди</param>
/// <param name="logger">Структурный логгер</param>
public class SqsProducerService(
    IAmazonSQS client,
    IConfiguration configuration,
    ILogger<SqsProducerService> logger) : IProducerService
{
    private readonly string _queueName = configuration["AWS:Resources:SQSQueueName"]
        ?? throw new KeyNotFoundException("SQS queue name was not found in configuration");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public async Task SendMessage(Course course)
    {
        try
        {
            var json = JsonSerializer.Serialize(course, SerializerOptions);
            var response = await client.SendMessageAsync(_queueName, json);

            if (response.HttpStatusCode == HttpStatusCode.OK)
                logger.LogInformation("Course {CourseId} was sent to file service via SQS", course.Id);
            else
                throw new Exception($"SQS returned {response.HttpStatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to send course {CourseId} through SQS queue", course.Id);
        }
    }
}
