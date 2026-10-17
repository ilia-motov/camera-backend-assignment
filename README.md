# Тестовое задание C#/.NET

Основное решение находится в ветке `main`:

- `Legacy` — исправленный метод загрузки событий на .NET Framework 4.8. Учитывает границы интервалов, фильтрует Confidence, убирает дубли и сортирует события. Разбор и ответ про постепенную миграцию — в [Legacy/REVIEW.md](Legacy/REVIEW.md).
- `Modern` — HTTP API на ASP.NET Core / .NET 10, EF Core и PostgreSQL. Возвращает ближайшие активные камеры.

Дополнительно в `feature/postgis-nearest-5m` реализован поиск в БД на 5 млн камер. Запуск и отличия описаны в Modern/POSTGIS.md на той ветке.

## Запуск main

Нужны .NET 10 SDK и Docker с Compose. Команды выполняются из корня проекта. Если запущена БД feature-ветки, перед переключением выполните `docker compose down` — данные сохранятся.

```powershell
git switch main
docker compose up -d --wait
dotnet run --project Modern/CameraApi --launch-profile http
```

Compose поднимает только PostgreSQL, API работает локально на `http://localhost:5080`. База доступна на `localhost:54329`: имя `cameras`, пользователь и пароль `camera_demo`.

При первом запуске [001-init.sql](Modern/database/001-init.sql) создаёт таблицу и добавляет 6 камер. Данные сохраняются в Docker-томе. Остановить БД: `docker compose down`.

Пример запроса:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/cameras/nearest `
  -ContentType 'application/json' `
  -Body '{"latitude":55.7558,"longitude":37.6173,"limit":3}'
```

Для начальных данных вернутся Center (0 км), Center-2 (0 км) и North (1.11 км). Другие запросы — в [CameraApi.http](Modern/CameraApi/CameraApi.http).

### Без Docker

На ветке `main` достаточно обычного PostgreSQL. Создайте БД `cameras` и пользователя, затем выполните:

```powershell
psql -h localhost -p 5432 -U camera_demo -d cameras -v ON_ERROR_STOP=1 -f Modern/database/001-init.sql
$env:ConnectionStrings__Cameras = 'Host=localhost;Port=5432;Database=cameras;Username=camera_demo;Password=your_password'
dotnet run --project Modern/CameraApi --launch-profile http
```

Строка подключения указывает на готовую БД; сама она не создаёт базу и таблицы.

## Swagger, Visual Studio и pgAdmin

Swagger доступен в режиме Development на `http://localhost:5080/swagger` в обеих ветках. Раскройте `POST /api/cameras/nearest`, нажмите **Try it out**, заполните JSON и нажмите **Execute**.

Для отладки откройте `CameraBackend.sln` в Visual Studio 2026, назначьте `CameraApi` запускаемым проектом, выберите профиль `http` и нажмите F5. БД должна быть запущена заранее; браузер откроет Swagger. Точки останова можно поставить в `CamerasController`, `NearestCameraService` и `PostgresCameraRepository`.

В установленном на компьютере pgAdmin создайте сервер (**Register → Server**). На вкладке Connection укажите: Host `localhost`, Port `54329`, Maintenance database `cameras`, Username `camera_demo`, Password `camera_demo`. Затем откройте базу `cameras` → **Query Tool** и выполняйте SQL. Настройки одинаковы для обеих веток; подключение ведёт к БД контейнера, запущенного в данный момент.

## Как работает Modern в main

- Контроллер проверяет обязательные поля: latitude от -90 до 90, longitude от -180 до 180, limit от 1 до 100. При ошибке возвращает 400.
- Репозиторий через EF Core получает активные камеры без отслеживания изменений (`AsNoTracking`).
- Сервис считает расстояния через отдельный `HaversineDistanceCalculator`, сортирует по расстоянию, затем по Id и берёт limit камер. Километры округляются только в ответе.
- Если камер меньше limit, возвращаются доступные; если нет — пустой массив. Отмена запроса передаётся до БД и проверяется при расчётах.

Для небольшого списка достаточно полной сортировки: `O(A log A)` по времени и `O(A)` по памяти. При 5 млн камер загрузка всех строк в C# становится дорогой — поэтому в дополнительной ветке поиск перенесён в PostGIS с пространственным индексом.

## Сборка и тесты

```powershell
dotnet build CameraBackend.sln -c Release
dotnet test Modern/CameraApi.Tests -c Release
```

Для сборки Legacy нужен Windows. В Modern 7 тестов, БД для них не нужна: три проверяют Haversine, остальные — выбор камер, валидацию и отмену. В feature-ветке проверки выбора заменены проверками передачи поиска в репозиторий.

При дополнительном времени я бы проверил Legacy с настоящим WCF-сервисом, добавил проверки PostgreSQL в CI и провёл нагрузочный тест API.
