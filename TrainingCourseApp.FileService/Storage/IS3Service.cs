using System.Text.Json.Nodes;

namespace TrainingCourseApp.FileService.Storage;

/// <summary>
/// Контракт работы с объектным хранилищем (S3-совместимым) для хранения
/// сериализованных курсов, поступающих из брокера сообщений.
/// </summary>
public interface IS3Service
{
    /// <summary>
    /// Загружает в бакет JSON-представление курса. Имя ключа формируется
    /// на основе идентификатора курса вида <c>course_{id}.json</c>.
    /// </summary>
    /// <param name="fileData">Корректный JSON-документ курса (как в очереди)</param>
    /// <returns><c>true</c>, если объект успешно записан</returns>
    public Task<bool> UploadFile(string fileData);

    /// <summary>
    /// Возвращает список ключей всех объектов в управляемом бакете.
    /// </summary>
    public Task<List<string>> GetFileList();

    /// <summary>
    /// Скачивает объект по ключу и возвращает его как разобранный JSON-узел.
    /// </summary>
    /// <param name="filePath">Ключ объекта в бакете</param>
    public Task<JsonNode> DownloadFile(string filePath);

    /// <summary>
    /// Создаёт бакет, если он отсутствует. Вызывается на старте приложения.
    /// </summary>
    public Task EnsureBucketExists();
}
