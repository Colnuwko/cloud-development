using TrainingCourse.Api.Models;

namespace TrainingCourse.Api.Messaging;

/// <summary>
/// Контракт продюсера, отправляющего сгенерированные курсы в брокер сообщений
/// для последующей десериализации файловым сервисом.
/// </summary>
public interface IProducerService
{
    /// <summary>
    /// Сериализует переданный курс в JSON и публикует его в брокер.
    /// </summary>
    /// <param name="course">Сгенерированный курс, который должен быть сохранён в объектное хранилище</param>
    /// <returns>Задача, завершающаяся после получения подтверждения от брокера</returns>
    Task SendMessage(Course course);
}
