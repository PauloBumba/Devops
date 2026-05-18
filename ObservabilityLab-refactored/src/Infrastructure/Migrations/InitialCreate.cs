using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using ObservabilityLab.Infrastructure.Data;

namespace ObservabilityLab.Infrastructure.Migrations;

// ─── Migration: InitialCreate ─────────────────────────────────────────────────
// Gerada com: dotnet ef migrations add InitialCreate --project src/Infrastructure
//             --startup-project src/Api --output-dir Migrations

[DbContext(typeof(AppDbContext))]
[Migration("20240101000001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id        = table.Column<int>(nullable: false)
                                 .Annotation("Npgsql:ValueGenerationStrategy",
                                     Npgsql.EntityFrameworkCore.PostgreSQL
                                           .Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name      = table.Column<string>(maxLength: 200, nullable: false),
                Price     = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                Category  = table.Column<string>(maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: t => t.PrimaryKey("PK_Products", x => x.Id));

        mb.CreateIndex("IX_Products_Category",  "Products", "Category");
        mb.CreateIndex("IX_Products_CreatedAt", "Products", "CreatedAt");
    }

    protected override void Down(MigrationBuilder mb)
        => mb.DropTable(name: "Products");
}

// ─── Migration model snapshot ─────────────────────────────────────────────────

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder mb)
    {
        mb.HasAnnotation("ProductVersion", "9.0.0")
          .HasAnnotation("Relational:MaxIdentifierLength", 63);

        mb.Entity("ObservabilityLab.Infrastructure.Data.Entities.Product", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("integer");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<decimal>("Price").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
            b.Property<string>("Category").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("Category");
            b.HasIndex("CreatedAt");
            b.ToTable("Products");
        });
    }
}
