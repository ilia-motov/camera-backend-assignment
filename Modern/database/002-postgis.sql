-- Выполнять после 001-init.sql. На сервере PostgreSQL должен быть установлен PostGIS.
BEGIN;
CREATE EXTENSION IF NOT EXISTS postgis;

-- Для существующих строк location заполнится автоматически и будет обновляться при изменении координат.
ALTER TABLE cameras ADD COLUMN IF NOT EXISTS location geography(Point,4326)
    GENERATED ALWAYS AS (ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography) STORED;

CREATE INDEX IF NOT EXISTS idx_cameras_active_location
    ON cameras USING gist (location) WHERE is_active;
COMMIT;
ANALYZE cameras;
