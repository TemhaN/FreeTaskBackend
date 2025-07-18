using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FreeTaskBackend.Migrations
{
    public partial class fix2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Создаём временный столбец для хранения преобразованных данных
            migrationBuilder.AddColumn<string[]>(
                name: "TempSkills",
                table: "Teams",
                type: "text[]",
                nullable: true);

            // Копируем данные из Skills (jsonb) в TempSkills (text[]), преобразуя JSON в массив
            migrationBuilder.Sql(
                @"UPDATE ""Teams"" 
                  SET ""TempSkills"" = CASE 
                    WHEN ""Skills"" IS NULL THEN ARRAY[]::text[]
                    ELSE ARRAY(SELECT jsonb_array_elements_text(""Skills"")) 
                  END;");

            // Удаляем старый столбец Skills
            migrationBuilder.DropColumn(
                name: "Skills",
                table: "Teams");

            // Переименовываем TempSkills в Skills
            migrationBuilder.RenameColumn(
                name: "TempSkills",
                table: "Teams",
                newName: "Skills");

            // Устанавливаем NOT NULL для Skills
            migrationBuilder.AlterColumn<string[]>(
                name: "Skills",
                table: "Teams",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Создаём временный столбец для хранения данных в jsonb
            migrationBuilder.AddColumn<string>(
                name: "TempSkills",
                table: "Teams",
                type: "jsonb",
                nullable: true);

            // Копируем данные из Skills (text[]) в TempSkills (jsonb)
            migrationBuilder.Sql(
                @"UPDATE ""Teams"" 
                  SET ""TempSkills"" = to_jsonb(""Skills"");");

            // Удаляем старый столбец Skills
            migrationBuilder.DropColumn(
                name: "Skills",
                table: "Teams");

            // Переименовываем TempSkills в Skills
            migrationBuilder.RenameColumn(
                name: "TempSkills",
                table: "Teams",
                newName: "Skills");

            // Устанавливаем NOT NULL для Skills
            migrationBuilder.AlterColumn<string>(
                name: "Skills",
                table: "Teams",
                type: "jsonb",
                nullable: false);
        }
    }
}