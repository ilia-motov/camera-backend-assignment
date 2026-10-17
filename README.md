# Тестовое задание C#/.NET

- `main` — исходное решение: Legacy и API с расчётом Haversine в C#.
- `feature/postgis-nearest-5m` — поиск ближайших камер в PostgreSQL через PostGIS и необязательный seed 5 млн активных камер.

Разбор Legacy находится в [Legacy/REVIEW.md](Legacy/REVIEW.md). В этой ветке Legacy не менялся.

## Запуск feature-ветки

Нужны .NET 10 SDK, PowerShell и работающий Docker Desktop с Linux-контейнерами. Из корня проекта:

```powershell
./Modern/database/setup-database.ps1
dotnet run --project Modern/CameraApi --launch-profile http
```

Скрипт поднимает PostgreSQL 16 с PostGIS 3.5, создаёт таблицу, добавляет 6 демонстрационных камер и применяет SQL-миграцию. Без флага большой seed не запускается. API работает локально на `http://localhost:5080`, в Compose находится только БД.

Подключение к БД: `localhost:54329`, база `cameras`, пользователь и пароль `camera_demo`. Это настройки для локального примера.

Добавить 5 млн активных камер:

```powershell
./Modern/database/setup-database.ps1 -Seed5Million
```

Генерация выполняется в PostgreSQL через `generate_series`, без создания миллионов объектов в C#. Координаты образуют воспроизводимую сетку по всему миру. У новых камер отрицательные Id от -1 до -5000000, чтобы не пересекаться с обычной последовательностью Id.

Начальные 6 камер сохраняются: всего получится 5 000 006 строк, из них 5 000 005 активных. Все **сгенерированные** камеры активны. Повторный seed пропускается по записи в `demo_seed_runs`; данные и отметка сохраняются одной транзакцией. При ошибке вставка откатывается. Скрипт не удаляет существующие камеры. Для заполнения потребуется несколько минут и свободное место для данных, индекса и WAL.

Используется отдельный том `camera-postgis-data`; исходный `camera-data` из `main` сохраняется. При переключении веток сначала выполните `docker compose down`, затем переключите ветку и запустите её Compose. Команда `down` сохраняет данные.

Пример HTTP-запроса:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/cameras/nearest `
  -ContentType 'application/json' `
  -Body '{"latitude":55.7558,"longitude":37.6173,"limit":3}'
```

Без большого seed ответ: Center (0 км), Center-2 (0 км), North (1.11 км). После seed среди ближайших могут оказаться сгенерированные камеры. Запросы также есть в [CameraApi.http](Modern/CameraApi/CameraApi.http).

### Локальный PostgreSQL без Docker

Установите PostGIS для своей версии PostgreSQL, создайте БД и пользователя. Пользователь, применяющий миграцию, должен иметь право включать расширение PostGIS.

```powershell
./Modern/database/setup-database.ps1 -Local -Database cameras -Username camera_demo
# Для большого набора добавьте -Seed5Million.
# Если psql отсутствует в PATH, укажите -PsqlPath 'C:/Program Files/PostgreSQL/16/bin/psql.exe'.

$env:ConnectionStrings__Cameras = 'Host=localhost;Port=5432;Database=cameras;Username=camera_demo;Password=your_password'
dotnet run --project Modern/CameraApi --launch-profile http
```

Для другого адреса БД у скрипта есть параметры `-DatabaseHost` и `-Port`. Пароль запрашивает `psql`; также поддерживается файл паролей PostgreSQL (`pgpass.conf` в Windows). Строка подключения API сама по себе не создаёт БД и таблицы.

## Что изменилось

[002-postgis.sql](Modern/database/002-postgis.sql) — SQL-миграция поверх исходной таблицы. Она включает расширение `postgis`, добавляет `location geography(Point,4326)` и частичный индекс GiST только для активных камер. Это SQL-миграция, не миграция EF Core: сохраняем простой способ управления схемой из исходного проекта. Её можно повторно выполнить, в том числе на БД из `main`.

`location` — хранимая вычисляемая колонка из `longitude, latitude`. PostgreSQL заполняет её для существующих строк и пересчитывает при обновлении координат. Отдельный код синхронизации не нужен. На большой существующей таблице добавление такой колонки переписывает таблицу, а создание обычного индекса блокирует запись на время построения; для тестового проекта миграция выполняется до нагрузки.

В EF Core подключён `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`, включён `UseNetTopologySuite()` и настроено сопоставление `Point` с `geography`.

Новый метод [GetNearestActiveAsync](Modern/CameraApi/Infrastructure/PostgresCameraRepository.cs) отправляет в БД запрос следующего вида:

```sql
SELECT id, name, ST_Distance(location, @point, false) / 1000
FROM (
    SELECT id, name, location, location <-> @point AS distance_m
    FROM cameras
    WHERE is_active
    ORDER BY location <-> @point
    FETCH FIRST @limit ROWS WITH TIES
) AS nearest
ORDER BY distance_m, id
LIMIT @limit;
```

Фильтр, выбор ближайших, ограничение количества и расчёт расстояния выполняются до `ToListAsync`, на стороне SQL. Точка и limit передаются параметрами. В C# приходит максимум 100 строк; сервис больше не загружает все камеры. Старый `GetActiveAsync` сохранён для сравнения и не вызывается endpoint.

Почему SQL через `Database.SqlQuery`, а не один LINQ с `DistanceKnn().ThenBy(Id)`? На PostgreSQL 16 проверка показала, что дополнительный Id внутри KNN-сортировки приводит к полному просмотру таблицы. Подзапрос использует GiST и берёт limit ближайших плюс все равные расстояния на границе (`WITH TIES`). Внешний запрос разрешает эти совпадения по Id и возвращает не больше limit строк. Простое ограничение подзапроса без `WITH TIES` могло бы потерять нужную камеру с меньшим Id.

SQL выполняется через EF Core; интерполяция `SqlQuery` создаёт параметры, а не вставляет значения в текст SQL. Результат проецируется прямо в `NearestCamera`, без отслеживания сущностей.

Для `geography` оператор `<->` и `ST_Distance(..., false)` используют сферу; результат в метрах делится на 1000. Haversine и три теста к нему оставлены как исходная реализация из ТЗ, но в этом HTTP-пути не используются. См. [Npgsql](https://www.npgsql.org/efcore/mapping/nts.html) и [PostGIS KNN](https://postgis.net/docs/geometry_distance_knn.html).

Контракт API не изменён: latitude от -90 до 90, longitude от -180 до 180, limit от 1 до 100. При равном расстоянии сортируем по Id, округляем километры только перед ответом. Если камер меньше limit, возвращаем доступные; если нет — пустой массив. `CancellationToken` доходит до запроса EF Core.

## Проверка

```powershell
dotnet build CameraBackend.sln -c Release
dotnet test Modern/CameraApi.Tests -c Release
```

Для сборки Legacy нужен Windows; reference assemblies .NET Framework 4.8 загружаются через NuGet.

В тестовом проекте по-прежнему 7 тестов: три для Haversine, передача параметров поиска в репозиторий, отмена до обращения к БД, валидация HTTP и отмена HTTP-запроса. Они запускаются без PostgreSQL.

Сборка Release и все 7 тестов проходят. Отдельно на настоящей БД проверены миграция исходных данных, HTTP-запрос на большом наборе, фильтрация активности, совпадения на границе limit, расстояния в километрах и пересчёт location при изменении координат.

Проверить план на настоящей БД после seed:

```powershell
Get-Content Modern/database/explain-nearest.sql -Raw | docker compose exec -T postgres `
  psql -U camera_demo -d cameras -v ON_ERROR_STOP=1
```

Ожидаем KNN `Index Scan` по `idx_cameras_active_location` с `Order By: location <-> ...` и `Limit`. Для сортировки одинаковых расстояний по Id возможен `Incremental Sort`. Наличие GiST само по себе не гарантирует такой план: его нужно проверять на своих данных. Большие группы камер с одинаковыми координатами могут увеличить работу сортировки.

На локальном Docker с PostgreSQL 16.9 / PostGIS 3.5.2 проверен набор из 5 000 006 строк. Seed занял около 97 секунд, таблица с индексами — 1064 МБ. Повторный seed пропущен, число строк не изменилось. В трёх запросах (Москва, около линии перемены дат, около полюса) план использовал GiST; время SQL на прогретых данных — около 0.27–1.16 мс при limit 3, 100 и 10. Прямой вариант `ORDER BY distance, id LIMIT 3` занял около 1.67 секунды и просмотрел всю таблицу. Это одиночные замеры на синтетических данных, не результат нагрузочного теста.

## Что улучшил бы дальше

- Проверил бы распределение камер и параллельную нагрузку, а не только синтетическую сетку.
- Для работающей большой БД отдельно спланировал бы заполнение колонки и построение индекса без длительной блокировки записи.
- Добавил бы PostgreSQL/PostGIS в CI для проверки реального SQL.
