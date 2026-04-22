using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Brainova.BLL.Services.Classes
{
    public class ReportQuestionService : IReportQuestionService
    {
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportQuestionService(IUnitOfWork uow, UserManager<ApplicationUser> userManager)
        {
            _uow = uow;
            _userManager = userManager;
        }

        public async Task AddAsync(string supervisorId, CreateReportQuestionRequest req)
        {
            await EnsureSupervisorExists(supervisorId);

            var repo = _uow.Repo<ReportQuestion>();
            var normalizedCode = req.Code.Trim().ToLower();

            bool exists = await repo.ExistsAsync(
                x => x.SupervisorId == supervisorId && x.Code == normalizedCode);

            if (exists)
                throw new BadRequestException("Question code already exists for this supervisor.");

            ValidateQuestionRequest(req);

            var q = new ReportQuestion
            {
                Id = Guid.NewGuid(),
                SupervisorId = supervisorId,
                Code = normalizedCode,
                Text = req.Text.Trim(),
                Type = req.Type,
                Order = req.Order,
                IsActive = req.IsActive,
                IsRequired = req.IsRequired,
                SkipWhenNoTumor = req.SkipWhenNoTumor,
                OptionsJson = req.Options != null && req.Options.Any()
                    ? JsonSerializer.Serialize(
                        req.Options
                            .Select(x => x.Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList())
                    : null
            };

            await repo.AddAsync(q);
            await _uow.SaveChangesAsync();
        }

        public async Task<List<ReportQuestion>> GetActiveForStudentAsync(string studentId)
        {
            var student = await _uow.Repo<ApplicationUser>()
                .Query()
                .FirstOrDefaultAsync(x => x.Id == studentId);

            if (student == null)
                throw new NotFoundException("Student not found");

            if (string.IsNullOrWhiteSpace(student.SupervisorUserId))
                throw new BadRequestException("Student has no assigned supervisor");

            return await _uow.Repo<ReportQuestion>()
                .Query()
                .Where(q => q.SupervisorId == student.SupervisorUserId && q.IsActive)
                .OrderBy(q => q.Order)
                .ToListAsync();
        }

        public async Task<List<ReportQuestion>> GetAllForSupervisorAsync(string supervisorId)
        {
            await EnsureSupervisorExists(supervisorId);

            return await _uow.Repo<ReportQuestion>()
                .Query()
                .Where(q => q.SupervisorId == supervisorId)
                .OrderBy(q => q.Order)
                .ToListAsync();
        }

        public async Task UpdateAsync(string supervisorId, Guid id, UpdateReportQuestionRequest req)
        {
            var repo = _uow.Repo<ReportQuestion>();
            var question = await repo.Query()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (question == null)
                throw new NotFoundException("Question not found");

            if (question.SupervisorId != supervisorId)
                throw new ForbiddenException("You are not allowed to update this question");

            var normalizedCode = req.Code.Trim().ToLower();

            var codeUsedByAnother = await repo.ExistsAsync(
                x => x.SupervisorId == supervisorId &&
                     x.Code == normalizedCode &&
                     x.Id != id);

            if (codeUsedByAnother)
                throw new BadRequestException("Question code already exists for this supervisor.");

            ValidateQuestionRequest(req);

            question.Code = normalizedCode;
            question.Text = req.Text.Trim();
            question.Type = req.Type;
            question.Order = req.Order;
            question.IsActive = req.IsActive;
            question.IsRequired = req.IsRequired;
            question.SkipWhenNoTumor = req.SkipWhenNoTumor;
            question.OptionsJson = req.Options != null && req.Options.Any()
                ? JsonSerializer.Serialize(
                    req.Options
                        .Select(x => x.Trim().ToLower())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList())
                : null;

            repo.Update(question);
            await _uow.SaveChangesAsync();
        }

        public async Task ToggleActiveAsync(string supervisorId, Guid id)
        {
            var repo = _uow.Repo<ReportQuestion>();
            var question = await repo.Query()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (question == null)
                throw new NotFoundException("Question not found");

            if (question.SupervisorId != supervisorId)
                throw new ForbiddenException("You are not allowed to update this question");

            question.IsActive = !question.IsActive;
            repo.Update(question);
            await _uow.SaveChangesAsync();
        }

        private async Task EnsureSupervisorExists(string supervisorId)
        {
            var supervisor = await _userManager.FindByIdAsync(supervisorId);
            if (supervisor == null)
                throw new NotFoundException("Supervisor not found");

            var roles = await _userManager.GetRolesAsync(supervisor);
            if (!roles.Contains("Supervisor"))
                throw new BadRequestException("Invalid supervisor");
        }

        private static void ValidateQuestionRequest(CreateReportQuestionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Code))
                throw new BadRequestException("Code is required.");

            if (string.IsNullOrWhiteSpace(req.Text))
                throw new BadRequestException("Text is required.");

            if (req.Type == ReportQuestionType.SingleChoice)
            {
                if (req.Options == null || req.Options.Count < 2)
                    throw new BadRequestException("Single choice question must have at least two options.");

                var cleaned = req.Options
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (cleaned.Count < 2)
                    throw new BadRequestException("Single choice question must have at least two valid options.");

                var duplicates = cleaned
                    .GroupBy(x => x.ToLower())
                    .Any(g => g.Count() > 1);

                if (duplicates)
                    throw new BadRequestException("Options cannot contain duplicates.");
            }
            else
            {
                if (req.Options != null && req.Options.Any(x => !string.IsNullOrWhiteSpace(x)))
                    throw new BadRequestException("Only single choice questions can have options.");
            }
        }

        private static void ValidateQuestionRequest(UpdateReportQuestionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Code))
                throw new BadRequestException("Code is required.");

            if (string.IsNullOrWhiteSpace(req.Text))
                throw new BadRequestException("Text is required.");

            if (req.Type == ReportQuestionType.SingleChoice)
            {
                if (req.Options == null || req.Options.Count < 2)
                    throw new BadRequestException("Single choice question must have at least two options.");

                var cleaned = req.Options
                    .Select(x => x.Trim().ToLower())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (cleaned.Count < 2)
                    throw new BadRequestException("Single choice question must have at least two valid options.");

                var duplicates = cleaned
                    .GroupBy(x => x)
                    .Any(g => g.Count() > 1);

                if (duplicates)
                    throw new BadRequestException("Options cannot contain duplicates.");
            }
            else
            {
                if (req.Options != null && req.Options.Any(x => !string.IsNullOrWhiteSpace(x)))
                    throw new BadRequestException("Only single choice questions can have options.");
            }
        }
        public async Task SeedDefaultQuestionsForSupervisorAsync(string supervisorId, CancellationToken ct = default)
        {
            await EnsureSupervisorExists(supervisorId);

            var repo = _uow.Repo<ReportQuestion>();

            var alreadyHasQuestions = await repo.Query()
                .AnyAsync(q => q.SupervisorId == supervisorId, ct);

            if (alreadyHasQuestions)
                return;

            var defaultQuestions = new List<ReportQuestion>
    {
        new ReportQuestion
        {
            Id = Guid.NewGuid(),
            SupervisorId = supervisorId,
            Code = "preliminary assesment",
            Text = "Based on your observation, what type of tumor do you think is shown in the MRI image?",
            Type = ReportQuestionType.SingleChoice,
            Order = 1,
            IsActive = true,
            IsRequired = true,
            OptionsJson = JsonSerializer.Serialize(new List<string>
            {
                "glioma",
                "meningioma",
                "pituitary",
                "no tumor"
            })
        },
        new ReportQuestion
        {
            Id = Guid.NewGuid(),
            SupervisorId = supervisorId,
            Code = "tumor size",
            Text = "What is the approximate size of the tumor?",
            Type = ReportQuestionType.SingleChoice,
            Order = 2,
            IsActive = true,
            IsRequired = true,
            SkipWhenNoTumor = true,
            OptionsJson = JsonSerializer.Serialize(new List<string>
            {
                "small",
                "medium",
                "large"
            })
        },
        new ReportQuestion
        {
            Id = Guid.NewGuid(),
            SupervisorId = supervisorId,
            Code = "tumor location",
            Text = "In which area of the brain does the abnormality most likely appear?",
            Type = ReportQuestionType.SingleChoice,
            Order = 3,
            IsActive = true,
            IsRequired = true,
            SkipWhenNoTumor = true,
            OptionsJson = JsonSerializer.Serialize(new List<string>
            {
                "frontal",
                "posterior",
                "central",
                "not clear"
            })
        },
        new ReportQuestion
        {
            Id = Guid.NewGuid(),
            SupervisorId = supervisorId,
            Code = "functional impact",
            Text = "Based on the tumor location, which brain function is most likely to be affected?",
            Type = ReportQuestionType.Text,
            Order = 4,
            IsActive = true,
            IsRequired = true,
            SkipWhenNoTumor = true,
            OptionsJson = null
        },
        new ReportQuestion
        {
            Id = Guid.NewGuid(),
            SupervisorId = supervisorId,
            Code = "additional explanation",
            Text = "Would you like to further explain your reasoning?",
            Type = ReportQuestionType.Text,
            Order = 5,
            IsActive = true,
            IsRequired = false,
            OptionsJson = null
        }
    };

            foreach (var question in defaultQuestions)
                await repo.AddAsync(question, ct);

            await _uow.SaveChangesAsync(ct);
        }
        public async Task SeedDefaultQuestionsForAllExistingSupervisorsAsync(CancellationToken ct = default)
        {
            var users = await _uow.Repo<ApplicationUser>()
                .Query()
                .ToListAsync(ct);

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("Supervisor"))
                    continue;

                await SeedDefaultQuestionsForSupervisorAsync(user.Id, ct);
            }
        }
    }
}