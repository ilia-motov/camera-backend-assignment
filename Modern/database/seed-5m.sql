-- Запускается явно через setup-database.ps1 -Seed5Million, а не при старте API.
\timing on
BEGIN;
-- Запускаем заполнение по очереди; отметку о завершении сохраняем вместе с добавленными строками.
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

    -- Отрицательные Id не пересекаются с положительной последовательностью. При конфликте заполнение прерывается.
    -- Воспроизводимая сетка: 2000 широт и 2500 долгот, все камеры активны.
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
