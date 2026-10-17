# Тестовое задание C#/.NET

В решении две части:

- `Legacy` — исправленный метод загрузки событий на .NET Framework 4.8. Разбор исходного кода и ответ про миграцию — в [Legacy/REVIEW.md](Legacy/REVIEW.md).
- `Modern` — API поиска ближайших камер на ASP.NET Core / .NET 10, EF Core и PostgreSQL.

## Как запустить

Нужны .NET 10 SDK и Docker с Compose. Для сборки Legacy используется Windows; reference assemblies .NET Framework 4.8 загружаются через NuGet.

Из корня проекта:

```sh
docker compose up -d --wait
dotnet run --project Modern/CameraApi --launch-profile http
```

API будет доступен на `http://localhost:5080`. База — на `localhost:54329`, имя БД `cameras`, пользователь и пароль `camera_demo`. Это настройки для локального запуска.

При первом запуске PostgreSQL выполняет [001-init.sql](Modern/database/001-init.sql): создаёт таблицу и добавляет несколько камер. При следующих запусках данные сохраняются. Если том уже создан, изменения SQL-скрипта нужно применить отдельно.

Пример запроса в PowerShell:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/cameras/nearest `
  -ContentType 'application/json' `
  -Body '{"latitude":55.7558,"longitude":37.6173,"limit":3}'
```

Для начальных данных вернутся:

```json
[
  { "id": 1, "name": "Center", "distanceKm": 0 },
  { "id": 6, "name": "Center-2", "distanceKm": 0 },
  { "id": 2, "name": "North", "distanceKm": 1.11 }
]
```

Запросы также есть в [CameraApi.http](Modern/CameraApi/CameraApi.http).

Остановить БД с сохранением данных:

```sh
docker compose down
```

### Если PostgreSQL уже установлен

Создайте БД `cameras` и пользователя, затем выполните скрипт:

```sh
psql -h localhost -p 5432 -U camera_demo -d cameras -v ON_ERROR_STOP=1 -f Modern/database/001-init.sql
```

Укажите свою строку подключения и запустите API. Например, в PowerShell:

```powershell
$env:ConnectionStrings__Cameras = 'Host=localhost;Port=5432;Database=cameras;Username=camera_demo;Password=your_password'
dotnet run --project Modern/CameraApi --launch-profile http
```

Схему создаёт SQL-скрипт. EF Core работает с готовой таблицей; применять миграции для запуска не нужно.

## Сборка и тесты

Сборка всего решения на Windows:

```sh
dotnet build CameraBackend.sln -c Release
```

Тесты Modern запускаются без PostgreSQL:

```sh
dotnet test Modern/CameraApi.Tests -c Release
```

Всего 7 тестов:

1. Одинаковые точки дают расстояние 0.
2. Расстояние симметрично.
3. Лондон–Нью-Йорк — примерно 5570 км.
4. Выбираются ближайшие камеры с учётом limit; при равных расстояниях — по Id.
5. Если камер меньше limit, возвращаются доступные.
6. Неверные координаты и limit дают 400 без обращения к репозиторию.
7. Отмена HTTP-запроса доходит до операции репозитория.

Первые пять — unit-тесты. Два остальных проверяют API через `WebApplicationFactory` с тестовым репозиторием.

## Основные решения

В Legacy сохранён WCF-контракт. `ICameraService` передаётся в конструктор загрузчика. Метод исправляет интервалы, отбрасывает события с `Confidence <= 0.7`, убирает дубли и сортирует результат. Подробности и принятые допущения — в [review](Legacy/REVIEW.md).

В Modern:

- Контроллер принимает запрос, сервис выбирает ближайшие камеры, репозиторий читает БД. Код разделён на папки `Controllers`, `Application` и `Infrastructure` внутри одного проекта.
- Все поля запроса обязательны. Latitude проверяется от -90 до 90, longitude — от -180 до 180, limit — от 1 до 100. Ошибки возвращаются как 400 с описанием полей.
- EF Core выбирает только активные камеры и нужные столбцы. Для чтения используется `AsNoTracking`. `DbContext` создаётся на время запроса и освобождается через DI.
- Расстояния считает отдельный `HaversineDistanceCalculator`. Формула использует сферу и возвращает километры.
- Камеры сортируются по точному расстоянию, затем по Id. Округление до двух знаков выполняется при подготовке ответа. Если камер нет, возвращается пустой массив.
- `CancellationToken` передаётся от контроллера до `ToListAsync` EF Core. Во время расчёта расстояний тоже проверяется отмена.

Для примерно 500 камер выбрана простая полная сортировка: `O(A log A)` по времени и `O(A)` по памяти, где `A` — число активных камер.

## Что изменится при 5 миллионах камер

При 5 млн камер я перенёс бы поиск из C# в PostgreSQL с PostGIS. База выбирала бы ближайшие камеры по пространственному индексу и возвращала только `limit` строк — без загрузки миллионов записей в приложение.

Для этого нужно:

1. Установить PostGIS и включить расширение `postgis` в БД.
2. Добавить `location` типа `geography(Point, 4326)`, заполнить из существующих координат и обновлять при их изменении. Порядок координат — **longitude, latitude**.
3. Создать GiST-индекс по `location` с условием `WHERE is_active`, чтобы индексировать только активные камеры.
4. Подключить `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`, включить `UseNetTopologySuite()` и сопоставить свойство `Point Location` с колонкой `location` типа `geography(Point, 4326)`.

Пример будущего запроса через EF Core:

```csharp
var point = new Point(longitude, latitude) { SRID = 4326 };

var cameras = await db.Cameras.AsNoTracking()
    .Where(c => c.IsActive)
    .OrderBy(c => EF.Functions.DistanceKnn(c.Location, point))
    .ThenBy(c => c.Id)
    .Take(limit)
    .Select(c => new
    {
        c.Id,
        c.Name,
        DistanceKm = EF.Functions.Distance(c.Location, point, false) / 1000
    })
    .ToListAsync(cancellationToken);
```

`DistanceKnn` переводится в оператор `<->` для KNN-поиска, а `Distance(..., false)` — в `ST_Distance(..., false)`. Расстояние возвращается в метрах и делится на 1000. Поддержка этих методов описана в [документации Npgsql](https://www.npgsql.org/efcore/mapping/nts.html).

Для `geography` KNN использует сферу. `ST_Distance` по умолчанию считает по эллипсоиду WGS84, а с `false` — по сфере, как Haversine в задании; небольшие численные отличия возможны из-за радиуса Земли. См. [KNN в PostGIS](https://postgis.net/docs/geometry_distance_knn.html) и [ST_Distance](https://postgis.net/docs/ST_Distance.html).

План проверил бы через `EXPLAIN (ANALYZE, BUFFERS)`: наличие индекса само по себе не гарантирует его использование. Этот вариант описан как дальнейшее развитие; в текущем API остаётся расчёт Haversine в C#.

## Что улучшил бы при дополнительном времени

- Проверил бы Legacy с настоящим WCF-сервисом: границы интервалов, timeout и восстановление клиента после ошибок.
- Добавил бы миграции EF Core при дальнейшем изменении схемы БД.
- Провёл бы нагрузочные проверки и настроил сборку и тесты в CI.

## Что проверено

Решение собирается в Release, все 7 тестов проходят. При переходе на EF Core отдельно проверены чтение из PostgreSQL и отмена заблокированного SQL-запроса. Настоящий WCF-сервис не проверялся, так как его адрес не предоставлен. Конфигурация Compose проверена, сама БД для проверок запускалась локально, без Docker.
