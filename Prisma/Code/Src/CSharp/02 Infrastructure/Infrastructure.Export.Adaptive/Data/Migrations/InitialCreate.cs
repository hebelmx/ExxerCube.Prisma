using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Export.Adaptive.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    TemplateId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TemplateType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    XmlNamespace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RootElement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.TemplateId);
                });

            migrationBuilder.CreateTable(
                name: "FieldMapping",
                columns: table => new
                {
                    TemplateDefinitionTemplateId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MappingId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFieldPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetField = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsNullable = table.Column<bool>(type: "bit", nullable: false),
                    TransformExpression = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldMapping", x => new { x.TemplateDefinitionTemplateId, x.Id });
                    table.ForeignKey(
                        name: "FK_FieldMapping_Templates_TemplateDefinitionTemplateId",
                        column: x => x.TemplateDefinitionTemplateId,
                        principalTable: "Templates",
                        principalColumn: "TemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Templates_TemplateType_IsActive_EffectiveDate",
                table: "Templates",
                columns: new[] { "TemplateType", "IsActive", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Templates_TemplateType_Version",
                table: "Templates",
                columns: new[] { "TemplateType", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldMapping");

            migrationBuilder.DropTable(
                name: "Templates");
        }
    }
}
