using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Minio;
using Minio.DataModel.Args;

namespace TrainingCourseApp.FileService.Storage;

/// <summary>
/// Реализация <see cref="IS3Service"/> поверх клиента MinIO. Используется в варианте
/// «SQS + MinIO» лабораторной работы №3. Имя бакета берётся из конфигурации
/// (<c>AWS:Resources:MinioBucketName</c>) и резолвится через Aspire MinIO-ресурс.
/// </summary>
/// <param name="client">Низкоуровневый клиент MinIO</param>
/// <param name="configuration">Конфигурация приложения</param>
/// <param name="logger">Структурный логгер</param>
public class S3MinioService(
    IMinioClient client,
    IConfiguration configuration,
    ILogger<S3MinioService> logger) : IS3Service
{
    private readonly string _bucketName = configuration["AWS:Resources:MinioBucketName"]
        ?? throw new KeyNotFoundException("S3 bucket name was not found in configuration");

    /// <inheritdoc />
    public async Task<List<string>> GetFileList()
    {
        var list = new List<string>();
        var request = new ListObjectsArgs()
            .WithBucket(_bucketName)
            .WithPrefix(string.Empty)
            .WithRecursive(true);

        logger.LogInformation("Began listing files in {Bucket}", _bucketName);
        var responseList = client.ListObjectsEnumAsync(request);

        await foreach (var response in responseList)
            list.Add(response.Key);

        return list;
    }

    /// <inheritdoc />
    public async Task<bool> UploadFile(string fileData)
    {
        var rootNode = JsonNode.Parse(fileData) ?? throw new ArgumentException("Passed string is not a valid JSON");
        var id = rootNode["id"]?.GetValue<int>() ?? throw new ArgumentException("Passed JSON has no 'id' property");

        var bytes = Encoding.UTF8.GetBytes(fileData);
        using var stream = new MemoryStream(bytes);

        logger.LogInformation("Began uploading course {CourseId} onto {Bucket}", id, _bucketName);

        var request = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithStreamData(stream)
            .WithObjectSize(bytes.Length)
            .WithObject($"course_{id}.json");

        var response = await client.PutObjectAsync(request);

        if (response.ResponseStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Failed to upload course {CourseId}: {StatusCode}", id, response.ResponseStatusCode);
            return false;
        }

        logger.LogInformation("Finished uploading course {CourseId} to {Bucket}", id, _bucketName);
        return true;
    }

    /// <inheritdoc />
    public async Task<JsonNode> DownloadFile(string filePath)
    {
        logger.LogInformation("Began downloading {Key} from {Bucket}", filePath, _bucketName);

        try
        {
            var memoryStream = new MemoryStream();
            var request = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithCallbackStream(async (stream, ct) =>
                {
                    await stream.CopyToAsync(memoryStream, ct);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                });

            var response = await client.GetObjectAsync(request)
                ?? throw new InvalidOperationException($"Empty response for {filePath}");

            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
            return JsonNode.Parse(reader.ReadToEnd())
                ?? throw new InvalidOperationException("Downloaded document is not a valid JSON");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during {Key} downloading", filePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task EnsureBucketExists()
    {
        logger.LogInformation("Checking whether {Bucket} exists", _bucketName);

        try
        {
            var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName));
            if (exists)
            {
                logger.LogInformation("{Bucket} already exists", _bucketName);
                return;
            }

            logger.LogInformation("Creating {Bucket}", _bucketName);
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred during {Bucket} check", _bucketName);
            throw;
        }
    }
}
