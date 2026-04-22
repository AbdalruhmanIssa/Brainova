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
            b.Property(x => x.Text).IsRequired().HasMaxLength(2000);
            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.Order).IsRequired();
            b.Property(x => x.IsActive).IsRequired();
            b.Property(x => x.IsRequired).IsRequired();
            b.Property(x => x.SkipWhenNoTumor).IsRequired().HasDefaultValue(false);
            b.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");

            b.Property(x => x.SupervisorId)
                .IsRequired()
                .HasMaxLength(450);

            // same code can exist under different supervisors
            b.HasIndex(x => new { x.SupervisorId, x.Code }).IsUnique();

            b.HasIndex(x => x.SupervisorId);

            b.HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}