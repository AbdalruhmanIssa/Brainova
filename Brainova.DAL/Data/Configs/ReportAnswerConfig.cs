using Brainova.DAL.Modles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brainova.DAL.Data.Configs
{
    public class ReportAnswerConfig : IEntityTypeConfiguration<ReportAnswer>
    {
        public void Configure(EntityTypeBuilder<ReportAnswer> b)
        {
            b.ToTable("ReportAnswers");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.Report)
                .WithMany(r => r.Answers)
                .HasForeignKey(x => x.ReportId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // prevent duplicates
            b.HasIndex(x => new { x.ReportId, x.QuestionId }).IsUnique();

            b.Property(x => x.QuestionTextSnapshot).IsRequired().HasMaxLength(2000);
            b.Property(x => x.QuestionTypeSnapshot).IsRequired();

            b.Property(x => x.AnswerValue).HasColumnType("nvarchar(max)");

        }
    }
}