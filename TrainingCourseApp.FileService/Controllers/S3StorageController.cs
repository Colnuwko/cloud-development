using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using TrainingCourseApp.FileService.Storage;

namespace TrainingCourseApp.FileService.Controllers;

/// <summary>
/// HTTP-фасад над объектным хранилищем. Используется в первую очередь
/// интеграционными тестами для верификации того, что курс, сгенерированный API,
/// действительно сохранён в S3.
/// </summary>
/// <param name="s3Service">Сервис работы с объектным хранилищем</param>
/// <param name="logger">Структурный логгер</param>
[ApiController]
[Route("api/s3")]
public class S3StorageController(IS3Service s3Service, ILogger<S3StorageController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает список ключей всех файлов, сохранённых в управляемом бакете.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<List<string>>> ListFiles()
    {
        logger.LogInformation("{Method} of {Controller} was called", nameof(ListFiles), nameof(S3StorageController));
        try
        {
            var list = await s3Service.GetFileList();
            logger.LogInformation("Got a list of {Count} files from bucket", list.Count);
            return Ok(list);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during {Method}", nameof(ListFiles));
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Возвращает содержимое файла по ключу как JSON-документ.
    /// </summary>
    /// <param name="key">Ключ объекта в бакете (например, <c>course_42.json</c>)</param>
    [HttpGet("{key}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<JsonNode>> GetFile(string key)
    {
        logger.LogInformation("{Method} of {Controller} was called for {Key}", nameof(GetFile), nameof(S3StorageController), key);
        try
        {
            var node = await s3Service.DownloadFile(key);
            logger.LogInformation("Received json of {Size} bytes", Encoding.UTF8.GetByteCount(node.ToJsonString()));
            return Ok(node);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during {Method}", nameof(GetFile));
            return BadRequest(ex.Message);
        }
    }
}
