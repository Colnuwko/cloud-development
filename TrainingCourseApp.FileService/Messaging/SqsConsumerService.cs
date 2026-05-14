using Amazon.SQS;
using Amazon.SQS.Model;
using TrainingCourseApp.FileService.Storage;

namespace TrainingCourseApp.FileService.Messaging;

/// <summary>
/// Фоновая служба, читающая сообщения из SQS батчами, передающая тело сообщения
/// в <see cref="IS3Service"/> для сохранения в объектное хранилище и удаляющая
/// успешно обработанные сообщения из очереди.
/// </summary>
/// <param name="sqsClient">Клиент SQS, сконфигурированный через LocalStack</param>
/// <param name="scopeFactory">Фабрика DI-скоупов для получения <see cref="IS3Service"/></param>
/// <param name="configuration">Конфигурация приложения, содержащая URL очереди</param>
/// <param name="logger">Структурный логгер</param>
public class SqsConsumerService(
    IAmazonSQS sqsClient,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SqsConsumerService> logger) : BackgroundService
{
    private readonly string _queueName = configuration["AWS:Resources:SQSQueueName"]
        ?? throw new KeyNotFoundException("SQS queue name was not found in configuration");

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SQS consumer service started for queue {Queue}", _queueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse? response;
            try
            {
                response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _queueName,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 5
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while receiving messages from {Queue}", _queueName);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            if (response?.Messages == null || response.Messages.Count == 0)
                continue;

            logger.LogInformation("Received {Count} messages from {Queue}", response.Messages.Count, _queueName);

            foreach (var message in response.Messages)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var s3Service = scope.ServiceProvider.GetRequiredService<IS3Service>();
                    await s3Service.UploadFile(message.Body);

                    await sqsClient.DeleteMessageAsync(_queueName, message.ReceiptHandle, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing message: {MessageId}", message.MessageId);
                }
            }
        }
    }
}
