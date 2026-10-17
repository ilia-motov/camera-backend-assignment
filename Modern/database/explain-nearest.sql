-- Выбор столбцов и сортировка повторяют запрос репозитория. Выполнять после подготовки и заполнения БД.
EXPLAIN (ANALYZE, BUFFERS)
SELECT nearest.id, nearest.name,
       ST_Distance(nearest.location, ST_SetSRID(ST_MakePoint(37.6173, 55.7558), 4326)::geography, false) / 1000 AS distance_km
FROM (
    SELECT id, name, location,
           location <-> ST_SetSRID(ST_MakePoint(37.6173, 55.7558), 4326)::geography AS distance_m
    FROM cameras
    WHERE is_active
    ORDER BY location <-> ST_SetSRID(ST_MakePoint(37.6173, 55.7558), 4326)::geography
    FETCH FIRST 3 ROWS WITH TIES
) AS nearest
ORDER BY nearest.distance_m, nearest.id
LIMIT 3;
