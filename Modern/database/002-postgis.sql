-- Run after 001-init.sql. PostGIS must be installed on the PostgreSQL server.
BEGIN;
CREATE EXTENSION IF NOT EXISTS postgis;

-- Existing rows are filled automatically; coordinate updates also update location.
ALTER TABLE cameras ADD COLUMN IF NOT EXISTS location geography(Point,4326)
    GENERATED ALWAYS AS (ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography) STORED;

CREATE INDEX IF NOT EXISTS idx_cameras_active_location
    ON cameras USING gist (location) WHERE is_active;
COMMIT;
ANALYZE cameras;
