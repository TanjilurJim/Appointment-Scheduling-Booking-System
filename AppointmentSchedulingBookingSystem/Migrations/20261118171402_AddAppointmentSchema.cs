using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppointmentSchedulingBookingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Consultants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Specialization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consultants", x => x.Id);
                    table.CheckConstraint("CK_Consultants_SlotDuration", "[SlotDurationMinutes] BETWEEN 1 AND 1440");
                    table.ForeignKey(
                        name: "FK_Consultants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ConsultantId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.CheckConstraint("CK_Appointment_Status", "[Status] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Appointment_Time", "[StartTime] < [EndTime]");
                    table.CheckConstraint("CK_Appointment_Timestamps", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_Appointments_Consultants_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "Consultants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsultantAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsultantId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultantAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_Availability_Day", "[DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_Availability_Time", "[StartTime] < [EndTime]");
                    table.ForeignKey(
                        name: "FK_ConsultantAvailabilities_Consultants_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "Consultants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsultantBreaks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsultantId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultantBreaks", x => x.Id);
                    table.CheckConstraint("CK_Break_Day", "[DayOfWeek] IS NULL OR [DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_Break_Schedule", "([DayOfWeek] IS NOT NULL AND [Date] IS NULL) OR ([DayOfWeek] IS NULL AND [Date] IS NOT NULL)");
                    table.CheckConstraint("CK_Break_Time", "[StartTime] < [EndTime]");
                    table.ForeignKey(
                        name: "FK_ConsultantBreaks_Consultants_ConsultantId",
                        column: x => x.ConsultantId,
                        principalTable: "Consultants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ConsultantId_Date_StartTime",
                table: "Appointments",
                columns: new[] { "ConsultantId", "Date", "StartTime" },
                unique: true,
                filter: "[Status] <> 1");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId_Date",
                table: "Appointments",
                columns: new[] { "CustomerId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantAvailabilities_ConsultantId_DayOfWeek",
                table: "ConsultantAvailabilities",
                columns: new[] { "ConsultantId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantBreaks_ConsultantId_Date",
                table: "ConsultantBreaks",
                columns: new[] { "ConsultantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultantBreaks_ConsultantId_DayOfWeek",
                table: "ConsultantBreaks",
                columns: new[] { "ConsultantId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_Consultants_UserId",
                table: "Consultants",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "ConsultantAvailabilities");

            migrationBuilder.DropTable(
                name: "ConsultantBreaks");

            migrationBuilder.DropTable(
                name: "Consultants");
        }
    }
}
