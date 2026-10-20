DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'payments') THEN
        CREATE SCHEMA payments;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS payments."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'payments') THEN
            CREATE SCHEMA payments;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE SEQUENCE payments.order_codes START WITH 1000000 INCREMENT BY 1 NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE TABLE payments.gateway_receipts (
        id uuid NOT NULL,
        gateway character varying(10) NOT NULL,
        reference character varying(100) NOT NULL,
        order_code bigint NOT NULL,
        amount numeric(18,0) NOT NULL,
        currency character varying(3) NOT NULL,
        rejection_code character varying(100),
        received_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_gateway_receipts" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE TABLE payments.payment_orders (
        id uuid NOT NULL,
        order_code bigint NOT NULL DEFAULT (nextval('payments.order_codes')),
        booking_id uuid NOT NULL,
        user_id uuid NOT NULL,
        mentor_user_id uuid NOT NULL,
        gateway character varying(10) NOT NULL,
        amount numeric(18,0) NOT NULL,
        currency character varying(3) NOT NULL,
        idempotency_key character varying(100) NOT NULL,
        request_hash character varying(64) NOT NULL,
        return_url character varying(2048) NOT NULL,
        cancel_url character varying(2048) NOT NULL,
        status character varying(30) NOT NULL,
        gateway_reference character varying(100),
        link_json jsonb,
        created_at_utc timestamp with time zone NOT NULL,
        expires_at_utc timestamp with time zone NOT NULL,
        verified_at_utc timestamp with time zone,
        CONSTRAINT "PK_payment_orders" PRIMARY KEY (id),
        CONSTRAINT ck_payment_amount CHECK (amount > 0 AND amount <= 1000000000 AND currency = 'VND')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE TABLE payments.wallets (
        id uuid NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_wallets" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE TABLE payments.escrows (
        id uuid NOT NULL,
        payment_id uuid NOT NULL,
        booking_id uuid NOT NULL,
        user_id uuid NOT NULL,
        mentor_user_id uuid NOT NULL,
        total_amount numeric(18,0) NOT NULL,
        currency character varying(3) NOT NULL,
        status character varying(30) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_escrows" PRIMARY KEY (id),
        CONSTRAINT ck_escrow_amount CHECK (total_amount > 0 AND status IN ('Holding','ReviewRequired')),
        CONSTRAINT "FK_escrows_payment_orders_payment_id" FOREIGN KEY (payment_id) REFERENCES payments.payment_orders (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE TABLE payments.ledger_entries (
        id uuid NOT NULL,
        payment_id uuid NOT NULL,
        account character varying(30) NOT NULL,
        amount numeric(18,0) NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ledger_entries" PRIMARY KEY (id),
        CONSTRAINT ck_ledger_account CHECK ((account = 'GatewayClearing' AND amount > 0) OR (account = 'EscrowLiability' AND amount < 0)),
        CONSTRAINT "FK_ledger_entries_payment_orders_payment_id" FOREIGN KEY (payment_id) REFERENCES payments.payment_orders (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE UNIQUE INDEX "IX_escrows_booking_id" ON payments.escrows (booking_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE UNIQUE INDEX "IX_escrows_payment_id" ON payments.escrows (payment_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE UNIQUE INDEX "IX_gateway_receipts_gateway_reference" ON payments.gateway_receipts (gateway, reference);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE UNIQUE INDEX "IX_ledger_entries_payment_id_account" ON payments.ledger_entries (payment_id, account);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE UNIQUE INDEX "IX_payment_orders_booking_id" ON payments.payment_orders (booking_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE UNIQUE INDEX "IX_payment_orders_gateway_gateway_reference" ON payments.payment_orders (gateway, gateway_reference);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE UNIQUE INDEX "IX_payment_orders_order_code" ON payments.payment_orders (order_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE INDEX "IX_payment_orders_status" ON payments.payment_orders (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    CREATE UNIQUE INDEX "IX_payment_orders_user_id_idempotency_key" ON payments.payment_orders (user_id, idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM payments."__EFMigrationsHistory" WHERE "MigrationId" = '20261020150619_AddPaymentsEscrow') THEN
    INSERT INTO payments."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020150619_AddPaymentsEscrow', '10.0.12');
    END IF;
END $EF$;
COMMIT;

