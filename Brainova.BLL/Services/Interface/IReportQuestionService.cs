using Brainova.BLL.DTOs.Request;
using Brainova.DAL.Modles;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brainova.BLL.Services.Interface
{
    public interface IReportQuestionService
    {
        Task AddAsync(CreateReportQuestionRequest req);
        Task<List<ReportQuestion>> GetActiveAsync();
        Task<List<ReportQuestion>> GetAllAsync();
        Task ToggleActiveAsync(Guid id);
    }
}