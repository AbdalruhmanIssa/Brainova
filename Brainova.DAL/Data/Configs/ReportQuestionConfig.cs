using Brainova.DAL.Modles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brainova.DAL.Data.Configs
{
    public class ReportQuestionConfig : IEntityTypeConfiguration<ReportQuestion>
    {
        public void Configure(EntityTypeBuilder<ReportQuestion> b)
        {
            b.ToTable("ReportQuestions");
            b.HasKey(x => x.Id);

            b.Property(x => x.Code).IsRequired().HasMaxLength(100);
            b.HasIndex(x => x.Code).IsUnique();

            b.Property(x => x.Text).IsRequired().HasMaxLength(2000);
            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.Order).IsRequired();

            b.Property(x => x.IsActive).IsRequired();
            b.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");
        }
    }
}