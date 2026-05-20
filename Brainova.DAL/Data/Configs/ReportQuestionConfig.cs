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
            b.Property(x => x.IsSystem).IsRequired().HasDefaultValue(false);
            b.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");

            b.Property(x => x.SupervisorId)
                .HasMaxLength(450);

            // same code can exist under different supervisors.
            // Filtered so multiple orphaned questions (SupervisorId = NULL) can coexist.
            b.HasIndex(x => new { x.SupervisorId, x.Code })
                .IsUnique()
                .HasFilter("[SupervisorId] IS NOT NULL");

            // order must be unique per supervisor (no two questions share the same order).
            // Filtered so orphaned questions don't conflict on uniqueness.
            b.HasIndex(x => new { x.SupervisorId, x.Order })
                .IsUnique()
                .HasFilter("[SupervisorId] IS NOT NULL");

            b.HasIndex(x => x.SupervisorId);

            b.HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}