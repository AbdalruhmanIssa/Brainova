using Brainova.DAL.Modles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brainova.DAL.Data.Configs
{
    public class ReportConfig : IEntityTypeConfiguration<Report>
    {
        public void Configure(EntityTypeBuilder<Report> b)
        {
            b.ToTable("Reports");
            b.HasKey(x => x.Id);

            b.Property(x => x.StudentId).IsRequired().HasMaxLength(450);
            b.Property(x => x.SubmittedAt).IsRequired();

            b.HasOne(x => x.Case)
                .WithMany()
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1 student -> 1 report -> 1 case
            b.HasIndex(x => new { x.CaseId, x.StudentId }).IsUnique();
        }
    }
}