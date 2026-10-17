-- Explicitly invoked by setup-database.ps1 -Seed5Million; never run at API startup.
\timing on
BEGIN;
-- Serialize seed attempts and commit the marker together with the inserted rows.
SELECT pg_advisory_xact_lock(150017, 5000000);
CREATE TABLE IF NOT EXISTS demo_seed_runs (
    name text PRIMARY KEY,
    completed_at timestamptz NOT NULL DEFAULT now()
);
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM demo_seed_runs WHERE name = 'active-cameras-5m-v1') THEN
        RAISE NOTICE '5 million demo cameras already seeded; skipping.';
        RETURN;
    END IF;

    -- Negative IDs avoid the positive identity sequence. Conflicts abort the seed.
    -- A reproducible grid: 2,000 latitudes x 2,500 longitudes, all active.
    INSERT INTO cameras (id, name, latitude, longitude, is_active)
    SELECT -n, 'Seed-' || n,
        -89.955 + ((n - 1) / 2500) * 0.09,
        -179.928 + ((n - 1) % 2500) * 0.144,
        true
    FROM generate_series(1, 5000000) AS s(n);

    INSERT INTO demo_seed_runs (name) VALUES ('active-cameras-5m-v1');
END $$;
COMMIT;
ANALYZE cameras;
