namespace TrainingCourse.Api.Services;

using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using TrainingCourse.Api.Messaging;
using TrainingCourse.Api.Models;

/// <summary>
/// Прикладной сервис обработки запросов к курсу.
/// Возвращает данные из распределённого кэша, а при их отсутствии генерирует новый курс,
/// публикует его в брокер сообщений (для последующей сериализации в S3) и помещает в кэш.
/// </summary>
/// <param name="cache">Распределённый кэш Redis, используемый для read-through кэширования</param>
/// <param name="producer">Продюсер брокера сообщений, доставляющий курс в файловый сервис</param>
/// <param name="configuration">Конфигурация приложения (TTL кэша)</param>
/// <param name="logger">Структурный логгер</param>
public class CourseService(
    IDistributedCache cache,
    IProducerService producer,
    IConfiguration configuration,
    ILogger<CourseService> logger) : ICourseService
{
    private readonly int _expirationMinutes = configuration.GetValue("CacheSettings:ExpirationMinutes", 10);

    /// <inheritdoc />
    public async Task<Course> GetCourse(int id)
    {
        var cacheKey = $"course-{id}";
        logger.LogInformation("Requesting course {CourseId} from cache", id);
        var cachedData = await cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            try
            {
                var cachedCourse = JsonSerializer.Deserialize<Course>(cachedData);

                if (cachedCourse != null)
                {
                    logger.LogInformation("Course {CourseId} retrieved from cache", id);
                    return cachedCourse;
                }
                logger.LogWarning("Course {CourseId} found in cache but deserialization returned null", id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deserialize course {CourseId} from cache", id);
            }
        }

        logger.LogInformation("Course {CourseId} not found in cache. Generating", id);

        var course = CourseGenerator.GenerateCourse(id);

        await producer.SendMessage(course);

        try
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_expirationMinutes)
            };
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(course), cacheOptions);
            logger.LogInformation("Course {CourseId} generated and cached", id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache course {CourseId}. Continuing without cache.", id);
        }
        return course;
    }
}
