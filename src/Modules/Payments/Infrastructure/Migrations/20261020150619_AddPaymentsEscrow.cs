using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Payments.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddPaymentsEscrow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "payments");

        migrationBuilder.CreateSequence(
            name: "order_codes",
            schema: "payments",
            startValue: 1000000L);

        migrationBuilder.CreateTable(
            name: "gateway_receipts",
            schema: "payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                gateway = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                order_code = table.Column<long>(type: "bigint", nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                rejection_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_gateway_receipts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "payment_orders",
            schema: "payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_code = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('payments.order_codes')"),
                booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                gateway = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                return_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                cancel_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                gateway_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                link_json = table.Column<string>(type: "jsonb", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_orders", x => x.id);
                table.CheckConstraint("ck_payment_amount", "amount > 0 AND amount <= 1000000000 AND currency = 'VND'");
            });

        migrationBuilder.CreateTable(
            name: "wallets",
            schema: "payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_wallets", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "escrows",
            schema: "payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                total_amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_escrows", x => x.id);
                table.CheckConstraint("ck_escrow_amount", "total_amount > 0 AND status IN ('Holding','ReviewRequired')");
                table.ForeignKey(
                    name: "FK_escrows_payment_orders_payment_id",
                    column: x => x.payment_id,
                    principalSchema: "payments",
                    principalTable: "payment_orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ledger_entries",
            schema: "payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                account = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ledger_entries", x => x.id);
                table.CheckConstraint("ck_ledger_account", "(account = 'GatewayClearing' AND amount > 0) OR (account = 'EscrowLiability' AND amount < 0)");
                table.ForeignKey(
                    name: "FK_ledger_entries_payment_orders_payment_id",
                    column: x => x.payment_id,
                    principalSchema: "payments",
                    principalTable: "payment_orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_escrows_booking_id",
            schema: "payments",
            table: "escrows",
            column: "booking_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_escrows_payment_id",
            schema: "payments",
            table: "escrows",
            column: "payment_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_gateway_receipts_gateway_reference",
            schema: "payments",
            table: "gateway_receipts",
            columns: new[] { "gateway", "reference" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ledger_entries_payment_id_account",
            schema: "payments",
            table: "ledger_entries",
            columns: new[] { "payment_id", "account" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payment_orders_booking_id",
            schema: "payments",
            table: "payment_orders",
            column: "booking_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payment_orders_gateway_gateway_reference",
            schema: "payments",
            table: "payment_orders",
            columns: new[] { "gateway", "gateway_reference" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payment_orders_order_code",
            schema: "payments",
            table: "payment_orders",
            column: "order_code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payment_orders_status",
            schema: "payments",
            table: "payment_orders",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_payment_orders_user_id_idempotency_key",
            schema: "payments",
            table: "payment_orders",
            columns: new[] { "user_id", "idempotency_key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "escrows",
            schema: "payments");

        migrationBuilder.DropTable(
            name: "gateway_receipts",
            schema: "payments");

        migrationBuilder.DropTable(
            name: "ledger_entries",
            schema: "payments");

        migrationBuilder.DropTable(
            name: "wallets",
            schema: "payments");

        migrationBuilder.DropTable(
            name: "payment_orders",
            schema: "payments");

        migrationBuilder.DropSequence(
            name: "order_codes",
            schema: "payments");
    }
}
