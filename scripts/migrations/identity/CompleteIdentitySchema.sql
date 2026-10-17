DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
        CREATE SCHEMA identity;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS identity."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20260911052416_Initial_Identity') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
            CREATE SCHEMA identity;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20260911052416_Initial_Identity') THEN
    CREATE TABLE identity.users (
        "Id" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "FullName" character varying(200) NOT NULL,
        "Role" character varying(50) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20260911052416_Initial_Identity') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON identity.users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20260911052416_Initial_Identity') THEN
    INSERT INTO identity."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260911052416_Initial_Identity', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    LOCK TABLE identity.users IN ACCESS EXCLUSIVE MODE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    DO $legacy_users$
    BEGIN
        IF EXISTS (
            SELECT 1 FROM identity.users
            WHERE ("Email" COLLATE "C") !~ '^[ -~]+$'
               OR length(btrim("Email")) = 0
        ) THEN
            RAISE EXCEPTION 'Legacy email values require a manual normalization review before this migration.';
        END IF;

        IF EXISTS (
            SELECT 1 FROM identity.users
            GROUP BY upper(btrim("Email") COLLATE "C")
            HAVING count(*) > 1
        ) THEN
            RAISE EXCEPTION 'Legacy emails collide after normalization; resolve them before this migration.';
        END IF;
    END
    $legacy_users$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ALTER COLUMN "IsActive" SET DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "AccessFailedCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "AvatarUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "IsEmailConfirmed" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "IsPhoneConfirmed" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "LockoutEnabled" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "LockoutEndUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "NormalizedEmail" character varying(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "PasswordHash" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "PhoneNumber" character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "SecurityStamp" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "TwoFactorEnabled" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ADD "UpdatedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    UPDATE identity.users
    SET "NormalizedEmail" = upper(btrim("Email") COLLATE "C"),
        "SecurityStamp" = replace(gen_random_uuid()::text, '-', ''),
        "PasswordHash" = '!RESET_REQUIRED!',
        "IsActive" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ALTER COLUMN "NormalizedEmail" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ALTER COLUMN "PasswordHash" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    ALTER TABLE identity.users ALTER COLUMN "SecurityStamp" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE TABLE identity.external_logins (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Provider" character varying(50) NOT NULL,
        "ProviderKey" character varying(200) NOT NULL,
        "ProviderDisplayName" character varying(100),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_external_logins" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_external_logins_users_UserId" FOREIGN KEY ("UserId") REFERENCES identity.users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE TABLE identity.refresh_tokens (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Token" character varying(500) NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "RevokedAtUtc" timestamp with time zone,
        "ReplacedByToken" character varying(500),
        "CreatedByIp" character varying(50),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_refresh_tokens_users_UserId" FOREIGN KEY ("UserId") REFERENCES identity.users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE TABLE identity.roles (
        "Id" uuid NOT NULL,
        "Name" character varying(50) NOT NULL,
        "NormalizedName" character varying(50) NOT NULL,
        "Description" character varying(250),
        CONSTRAINT "PK_roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE TABLE identity.user_roles (
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        CONSTRAINT "PK_user_roles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_user_roles_roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES identity.roles ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_user_roles_users_UserId" FOREIGN KEY ("UserId") REFERENCES identity.users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE UNIQUE INDEX "IX_users_NormalizedEmail" ON identity.users ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE UNIQUE INDEX "IX_external_logins_Provider_ProviderKey" ON identity.external_logins ("Provider", "ProviderKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE INDEX "IX_external_logins_UserId" ON identity.external_logins ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_Token" ON identity.refresh_tokens ("Token");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE INDEX "IX_refresh_tokens_UserId" ON identity.refresh_tokens ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE UNIQUE INDEX "IX_roles_Name" ON identity.roles ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE UNIQUE INDEX "IX_roles_NormalizedName" ON identity.roles ("NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    CREATE INDEX "IX_user_roles_RoleId" ON identity.user_roles ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity."__EFMigrationsHistory" WHERE "MigrationId" = '20261017173446_CompleteIdentitySchema') THEN
    INSERT INTO identity."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017173446_CompleteIdentitySchema', '10.0.12');
    END IF;
END $EF$;
COMMIT;

