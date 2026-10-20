DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'recommendations') THEN
        CREATE SCHEMA recommendations;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS recommendations."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'recommendations') THEN
            CREATE SCHEMA recommendations;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    CREATE TABLE recommendations.interactions (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        mentor_id uuid NOT NULL,
        skill_id uuid,
        interaction_type character varying(20) NOT NULL,
        deduplication_key character varying(64) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_interactions" PRIMARY KEY (id),
        CONSTRAINT ck_interaction_type CHECK (interaction_type IN ('Click','ViewProfile'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    CREATE TABLE recommendations.mentor_metrics (
        id uuid NOT NULL,
        mentor_user_id uuid NOT NULL,
        skill_ids uuid[] NOT NULL,
        prices jsonb NOT NULL,
        acceptance_rate double precision,
        completion_rate double precision,
        average_response_time_minutes double precision,
        repeat_mentee_count integer NOT NULL,
        total_sessions integer NOT NULL,
        average_rating double precision,
        total_reviews integer NOT NULL,
        last_calculated_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_mentor_metrics" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    CREATE TABLE recommendations.recommendation_logs (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        algorithm_version character varying(30) NOT NULL,
        mentor_ids uuid[] NOT NULL,
        scores double precision[] NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_recommendation_logs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    CREATE UNIQUE INDEX "IX_interactions_deduplication_key" ON recommendations.interactions (deduplication_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    CREATE INDEX "IX_interactions_skill_id_created_at_utc" ON recommendations.interactions (skill_id, created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    CREATE INDEX "IX_interactions_user_id_created_at_utc" ON recommendations.interactions (user_id, created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    CREATE INDEX "IX_mentor_metrics_mentor_user_id" ON recommendations.mentor_metrics (mentor_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    CREATE INDEX "IX_recommendation_logs_created_at_utc" ON recommendations.recommendation_logs (created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recommendations."__EFMigrationsHistory" WHERE "MigrationId" = '20261020152552_AddRecommendationReadModels') THEN
    INSERT INTO recommendations."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020152552_AddRecommendationReadModels', '10.0.12');
    END IF;
END $EF$;
COMMIT;

