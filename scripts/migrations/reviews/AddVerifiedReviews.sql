DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'reviews') THEN
        CREATE SCHEMA reviews;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS reviews."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reviews."__EFMigrationsHistory" WHERE "MigrationId" = '20261020151813_AddVerifiedReviews') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'reviews') THEN
            CREATE SCHEMA reviews;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reviews."__EFMigrationsHistory" WHERE "MigrationId" = '20261020151813_AddVerifiedReviews') THEN
    CREATE TABLE reviews.outbox_messages (
        id uuid NOT NULL,
        type character varying(100) NOT NULL,
        content jsonb NOT NULL,
        occurred_on_utc timestamp with time zone NOT NULL,
        processed_on_utc timestamp with time zone,
        error text,
        CONSTRAINT "PK_outbox_messages" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reviews."__EFMigrationsHistory" WHERE "MigrationId" = '20261020151813_AddVerifiedReviews') THEN
    CREATE TABLE reviews.reviews (
        id uuid NOT NULL,
        booking_id uuid NOT NULL,
        session_id uuid NOT NULL,
        mentor_id uuid NOT NULL,
        mentor_user_id uuid NOT NULL,
        mentee_user_id uuid NOT NULL,
        overall_rating integer NOT NULL,
        communication_rating integer NOT NULL,
        expertise_rating integer NOT NULL,
        helpfulness_rating integer NOT NULL,
        comment character varying(4000) NOT NULL,
        mentor_reply character varying(4000),
        mentor_replied_at_utc timestamp with time zone,
        is_visible boolean NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        updated_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_reviews" PRIMARY KEY (id),
        CONSTRAINT ck_review_ratings CHECK (overall_rating BETWEEN 1 AND 5 AND communication_rating BETWEEN 1 AND 5 AND expertise_rating BETWEEN 1 AND 5 AND helpfulness_rating BETWEEN 1 AND 5)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reviews."__EFMigrationsHistory" WHERE "MigrationId" = '20261020151813_AddVerifiedReviews') THEN
    CREATE UNIQUE INDEX "IX_reviews_booking_id" ON reviews.reviews (booking_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reviews."__EFMigrationsHistory" WHERE "MigrationId" = '20261020151813_AddVerifiedReviews') THEN
    CREATE INDEX "IX_reviews_mentee_user_id" ON reviews.reviews (mentee_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reviews."__EFMigrationsHistory" WHERE "MigrationId" = '20261020151813_AddVerifiedReviews') THEN
    CREATE INDEX "IX_reviews_mentor_id_is_visible_created_at_utc" ON reviews.reviews (mentor_id, is_visible, created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reviews."__EFMigrationsHistory" WHERE "MigrationId" = '20261020151813_AddVerifiedReviews') THEN
    INSERT INTO reviews."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020151813_AddVerifiedReviews', '10.0.12');
    END IF;
END $EF$;
COMMIT;

