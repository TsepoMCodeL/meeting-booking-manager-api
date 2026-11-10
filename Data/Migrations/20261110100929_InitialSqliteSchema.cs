using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingManagerAPI.Data.Migrations
{
    // Creates the initial meeting-room and booking tables.
    /// <inheritdoc />
    public partial class InitialSqliteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE "MeetingRooms" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_MeetingRooms" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "Capacity" INTEGER NOT NULL
                );

                CREATE TABLE "MeetingManagers" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_MeetingManagers" PRIMARY KEY AUTOINCREMENT,
                    "MeetingRoomId" INTEGER NOT NULL,
                    "MeetingRoomIsBooked" INTEGER NOT NULL,
                    "MeetingTitle" TEXT NOT NULL,
                    "MeetingStartTime" TEXT NOT NULL,
                    "MeetingEndDate" TEXT NOT NULL
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("MeetingManagers");
            migrationBuilder.DropTable("MeetingRooms");
        }
    }
}
