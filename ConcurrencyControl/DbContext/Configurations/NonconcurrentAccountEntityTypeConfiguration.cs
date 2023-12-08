using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConcurrencyControl.DbContext.Configurations;

internal class NonConcurrentAccountEntityTypeConfigurationSqlite : IEntityTypeConfiguration<NonConcurrentAccount>
{
    public void Configure(EntityTypeBuilder<NonConcurrentAccount> builder)
    {
        builder.ToTable("NonConcurrentAccounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(x => x.Balance).HasColumnName("Balance").HasConversion<double>();
    }
}

internal class NonConcurrentAccountEntityTypeConfiguration : IEntityTypeConfiguration<NonConcurrentAccount>
{
    public void Configure(EntityTypeBuilder<NonConcurrentAccount> builder)
    {
        builder.ToTable("NonConcurrentAccounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        builder.Property(x => x.Balance).HasColumnName("Balance").HasColumnType("money");
    }
}