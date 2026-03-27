using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Brainova.BLL.Services.Classes
{
    public class ReportQuestionService : IReportQuestionService
    {
        private readonly IUnitOfWork _uow;

        public ReportQuestionService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task AddAsync(CreateReportQuestionRequest req)
        {
            var repo = _uow.Repo<ReportQuestion>();

            var normalizedCode = req.Code.Trim().ToLower();

            bool exists = await repo.ExistsAsync(x => x.Code == normalizedCode);
            if (exists)
                throw new BadRequestException("Question code already exists.");

            ValidateQuestionRequest(req);

            var q = new ReportQuestion
            {
                Id = Guid.NewGuid(),
                Code = normalizedCode,
                Text = req.Text.Trim(),
                Type = req.Type,
                Order = req.Order,
                IsActive = req.IsActive,
                OptionsJson = req.Options != null && req.Options.Any()
                    ? JsonSerializer.Serialize(req.Options.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList())
                    : null
            };

            await repo.AddAsync(q);
            await _uow.SaveChangesAsync();
        }

        public async Task<List<ReportQuestion>> GetActiveAsync()
        {
            return await _uow.Repo<ReportQuestion>()
                .Query()
                .Where(q => q.IsActive)
                .OrderBy(q => q.Order)
                .ToListAsync();
        }

        public async Task<List<ReportQuestion>> GetAllAsync()
        {
            return await _uow.Repo<ReportQuestion>()
                .Query()
                .OrderBy(q => q.Order)
                .ToListAsync();
        }

        public async Task ToggleActiveAsync(Guid id)
        {
            var question = await _uow.Repo<ReportQuestion>().GetByIdAsync(id);

            if (question == null)
                throw new NotFoundException("Question not found");

            question.IsActive = !question.IsActive;
            _uow.Repo<ReportQuestion>().Update(question);

            await _uow.SaveChangesAsync();
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
    }
}