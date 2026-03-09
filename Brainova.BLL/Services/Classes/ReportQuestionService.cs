
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Mapster;
using Microsoft.EntityFrameworkCore;

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

            bool exists = await repo.ExistsAsync(x => x.Code == req.Code);
            if (exists)
                throw new Exception("Question code already exists.");
            var q = req.Adapt<ReportQuestion>();
            q.Id = Guid.NewGuid();
            await _uow.Repo<ReportQuestion>().AddAsync(q);
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
                throw new Exception("Question not found");

            question.IsActive = !question.IsActive;

            _uow.Repo<ReportQuestion>().Update(question);

            await _uow.SaveChangesAsync();
        }
    }
}