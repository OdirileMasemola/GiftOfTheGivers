using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GiftOfTheGivers.IntegrationTests.Infrastructure;

/// <summary>
/// SQLite has no decimal type and cannot SUM decimal columns, while Azure SQL can.
/// For the test database only, money columns are stored as REAL so the dashboard totals still run.
/// </summary>
public class SqliteDecimalModelCustomizer : RelationalModelCustomizer
{
    public SqliteDecimalModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(entity => entity.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetProviderClrType(typeof(double));
        }
    }
}
