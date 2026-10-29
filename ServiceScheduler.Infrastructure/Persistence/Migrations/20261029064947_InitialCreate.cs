using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceScheduler.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capabilities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dealerships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dealerships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_types", x => x.id);
                    table.CheckConstraint("ck_service_types_duration_positive", "duration > interval '0'");
                });

            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicles_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_bays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_bays", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_bays_dealerships_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technicians",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technicians", x => x.id);
                    table.ForeignKey(
                        name: "FK_technicians_dealerships_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "working_hours",
                columns: table => new
                {
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_working_hours", x => new { x.dealership_id, x.day_of_week });
                    table.CheckConstraint("ck_working_hours_opens_before_closes", "opens_at < closes_at");
                    table.ForeignKey(
                        name: "FK_working_hours_dealerships_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_type_required_capabilities",
                columns: table => new
                {
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_type_required_capabilities", x => new { x.service_type_id, x.capability_id });
                    table.ForeignKey(
                        name: "FK_service_type_required_capabilities_capabilities_capability_~",
                        column: x => x.capability_id,
                        principalTable: "capabilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_type_required_capabilities_service_types_service_ty~",
                        column: x => x.service_type_id,
                        principalTable: "service_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_type_required_skills",
                columns: table => new
                {
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_type_required_skills", x => new { x.service_type_id, x.skill_id });
                    table.ForeignKey(
                        name: "FK_service_type_required_skills_service_types_service_type_id",
                        column: x => x.service_type_id,
                        principalTable: "service_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_type_required_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_bay_capabilities",
                columns: table => new
                {
                    service_bay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_bay_capabilities", x => new { x.service_bay_id, x.capability_id });
                    table.ForeignKey(
                        name: "FK_service_bay_capabilities_capabilities_capability_id",
                        column: x => x.capability_id,
                        principalTable: "capabilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_bay_capabilities_service_bays_service_bay_id",
                        column: x => x.service_bay_id,
                        principalTable: "service_bays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_bay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.id);
                    table.CheckConstraint("ck_appointments_end_after_start", "end_at > start_at");
                    table.ForeignKey(
                        name: "FK_appointments_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_dealerships_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_service_bays_service_bay_id",
                        column: x => x.service_bay_id,
                        principalTable: "service_bays",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_service_types_service_type_id",
                        column: x => x.service_type_id,
                        principalTable: "service_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_technicians_technician_id",
                        column: x => x.technician_id,
                        principalTable: "technicians",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technician_skills",
                columns: table => new
                {
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_technician_skills", x => new { x.technician_id, x.skill_id });
                    table.ForeignKey(
                        name: "FK_technician_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_technician_skills_technicians_technician_id",
                        column: x => x.technician_id,
                        principalTable: "technicians",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_customer_id",
                table: "appointments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_dealership_id",
                table: "appointments",
                column: "dealership_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_service_bay_id",
                table: "appointments",
                column: "service_bay_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_service_type_id",
                table: "appointments",
                column: "service_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_technician_id",
                table: "appointments",
                column: "technician_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_vehicle_id",
                table: "appointments",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_capabilities_name",
                table: "capabilities",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_bay_capabilities_capability_id",
                table: "service_bay_capabilities",
                column: "capability_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_bays_dealership_id",
                table: "service_bays",
                column: "dealership_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_type_required_capabilities_capability_id",
                table: "service_type_required_capabilities",
                column: "capability_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_type_required_skills_skill_id",
                table: "service_type_required_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "IX_skills_name",
                table: "skills",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_technician_skills_skill_id",
                table: "technician_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "IX_technicians_dealership_id",
                table: "technicians",
                column: "dealership_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_customer_id",
                table: "vehicles",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_vin",
                table: "vehicles",
                column: "vin",
                unique: true);

            // EF Core cannot model exclusion constraints. Only Confirmed appointments reserve a resource;
            // '[)' makes the range half-open, so back-to-back appointments do not conflict.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                ALTER TABLE appointments ADD CONSTRAINT ex_appointments_technician_no_overlap
                EXCLUDE USING gist (technician_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)
                WHERE (status = 'Confirmed');
                """);
            migrationBuilder.Sql("""
                ALTER TABLE appointments ADD CONSTRAINT ex_appointments_service_bay_no_overlap
                EXCLUDE USING gist (service_bay_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)
                WHERE (status = 'Confirmed');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table drops its exclusion constraints; the extension is left installed (it may be shared).
            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "service_bay_capabilities");

            migrationBuilder.DropTable(
                name: "service_type_required_capabilities");

            migrationBuilder.DropTable(
                name: "service_type_required_skills");

            migrationBuilder.DropTable(
                name: "technician_skills");

            migrationBuilder.DropTable(
                name: "working_hours");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "service_bays");

            migrationBuilder.DropTable(
                name: "capabilities");

            migrationBuilder.DropTable(
                name: "service_types");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropTable(
                name: "technicians");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "dealerships");
        }
    }
}
