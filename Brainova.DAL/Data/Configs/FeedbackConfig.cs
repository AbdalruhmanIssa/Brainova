using Brainova.DAL.Modles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brainova.DAL.Data.Configs
{
    public class FeedbackConfig : IEntityTypeConfiguration<Feedback>
    {
        public void Configure(EntityTypeBuilder<Feedback> b)
        {
            b.ToTable("Feedbacks");
            b.HasKey(x => x.Id);

            b.Property(x => x.Comment)
                .IsRequired()
                .HasMaxLength(3000);

            b.Property(x => x.SupervisorId)
                .IsRequired()
                .HasMaxLength(450);

            b.Property(x => x.StudentId)
                .IsRequired()
                .HasMaxLength(450);

            b.HasOne(x => x.Report)
                .WithMany()
                .HasForeignKey(x => x.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ReportId).IsUnique();

        }
    }
}