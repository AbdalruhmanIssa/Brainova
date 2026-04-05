using Brainova.DAL.Data.Configs;
using Brainova.DAL.Modles;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace Brainova.DAL.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<MriCase> MriCases => Set<MriCase>();
        public DbSet<AiResult> AiResults => Set<AiResult>();
        public DbSet<Report> Reports => Set<Report>();
        public DbSet<ReportQuestion> ReportQuestions => Set<ReportQuestion>();
        public DbSet<ReportAnswer> ReportAnswers => Set<ReportAnswer>();
      //  public DbSet<Feedback> Feedbacks => Set<Feedback>();
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public override int SaveChanges()
        {
            ApplyBaseEntityRules();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyBaseEntityRules();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyBaseEntityRules()
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.Id == Guid.Empty)
                        entry.Entity.Id = Guid.NewGuid();

                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfiguration(new ReportConfig());
            builder.ApplyConfiguration(new ReportQuestionConfig());
            builder.ApplyConfiguration(new ReportAnswerConfig());
         builder.ApplyConfiguration(new FeedbackConfig());


            builder.Entity<ApplicationUser>()
                .HasOne(u => u.SupervisorUser)
                .WithMany(s => s.Students)
                .HasForeignKey(u => u.SupervisorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<MriCase>(e =>
            {
                e.ToTable("MriCases");
                e.HasKey(x => x.Id);

                e.Property(x => x.StoredFileName)
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.Status)
                    .HasConversion<int>()
                    .IsRequired();

                e.HasOne(x => x.Student)
        .WithMany()
        .HasForeignKey(x => x.StudentId)
        .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<AiResult>(e =>
            {
                e.ToTable("AiResults");
                e.HasKey(x => x.Id);

                e.Property(x => x.PredictionResult)
                    .IsRequired()
                    .HasMaxLength(100);

                e.Property(x => x.ProbabilitiesJson)
                    .IsRequired();

                e.Property(x => x.GradcamFileName)
                    .IsRequired()
                    .HasMaxLength(200);

               

                // 1-1: AiResult belongs to exactly 1 case, and each case has max 1 AiResult
                e.HasIndex(x => x.CaseId).IsUnique();

                e.HasOne(x => x.MriCase)
  .WithOne(c => c.AiResult)
  .HasForeignKey<AiResult>(x => x.CaseId)
  .OnDelete(DeleteBehavior.Restrict);
            });

            // other configs...
        }
    }
}
