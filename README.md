# Лабораторная работа №3 — «Объектное хранилище и брокер сообщений»

## Вариант 16 — «Учебный курс»

## Описание

Спроектирован и реализован микросервисный бэкенд для генерации данных учебных курсов. В проекте используется Redis для кэширования запросов, оркестрация микросервисов выполняется средствами .NET Aspire. Также были добавлены объектное хранилище, файловый сервис. Настроена асинхронная передача сообщений через брокер и написаны интеграционные тесты.


## Реализовано

- Сервис генерации данных Учебных курсов на основе Bogus
- Кэширование ответов при помощи `IDistributedCache` и Redis
- Структурное логирование сервиса генерации
- Оркестрация всех компонентов через .NET Aspire
- Blazor WebAssembly клиент для отображения данных
- Кастомный балансировщик нагрузки Weighted Round Robin для API Gateway
- Структурное логирование сервисов
- Реализован файловый сервис
- Добавлено объектное хранилище

## Стек технологий

- .NET 8
- .NET Aspire 9.3.1
- Bogus (v35.6.5) — генерация данных
- Redis (v9.3.1) — распределённое кэширование
- RedisInsight — визуализация данных в Redis
- Blazor WebAssembly — клиентское приложение
- Ocelot — API Gateway
- Minio — Объектное хранилище
- AWS SQS — Брокер сообщений
- xUnit — Тесты


## Структура проекта

| Проект | Описание |
|---|---|
| `TrainingCourse.Api` | API-сервис генерации курсов |
| `TrainingCourse.AppHost` | Aspire-оркестратор |
| `TrainingCourse.ServiceDefaults` | Общие настройки сервисов |
| `Client.Wasm` | Клиент |
| `TrainingCourseApp.Gateway` | API Gateway с кастомным балансировщиком |
| `TrainingCourseApp.FileService` | Файловый сервис – читает сообщения из SQS, сохраняет в Minio |
| `TrainingCourseApp.AppHost.Tests` | Интеграционные тесты|


## Характеристики создаваемых курсов

| № | Название | Тип данных | Источник Bogus |
|---|---|---|---|
| 1 | Идентификатор в системе | int | Параметр запроса |
| 2 | Название курса | string | Company.CatchPhrase() + " course" |
| 3 | ФИО преподавателя | string | Name.FullName() |
| 4 | Дата начала | DateOnly | f.Date.FutureDateOnly(1) |
| 5 | Дата окончания | DateOnly | Date.Between(StartDate, StartDate.AddMonths(6)) |
| 6 | Макс. количество студентов | int | Random.Int(10, 50) |
| 7 | Текущее количество студентов | int | Random.Int(0, MaxStudents) |
| 8 | Наличие сертификата | bool | Random.Bool(0.8f) |
| 9 | Стоимость курса | decimal | Math.Round(Random.Decimal(5000, 50000), 2) |
| 10 | Рейтинг курса | int | Random.Int(1, 5) |

## Балансировка нагрузки

Используется балансировщик нагрузки **Weighted Round Robin** для API Gateway, который обеспечивает распределение запросов между несколькими экземплярами сервиса `TrainingCourse.Api` с учётом их весовых коэффициентов. Чем выше вес указан у сервера, тем чаще он будет получаться запросы.

## Скриншоты

### Aspire Dashboard
![Aspire Dashboard](Images/AspireAllServices.png)

### Клиент
![Client](Images/WorkApp.png)

### Кэширование
![Cache](Images/WorkCaсhe.png)

### Балансировка
![Balance1](Images/WorkBalancer8000.png)
![Balance1](Images/WorkBalancer8001.png)
![Balance1](Images/WorkBalancer8002.png)

### Логи
![FileService](Images/LogsFileService.png)

### Тесты
![Tests](Images/Tests.png)