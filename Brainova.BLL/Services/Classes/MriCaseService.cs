using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Mapster;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Classes
{
    public class MriCaseService : IMriCaseService
    {
        private readonly IUnitOfWork _uow;
        private readonly IFileService _fileService;

        public MriCaseService(IUnitOfWork uow, IFileService fileService)
        {
            _uow = uow;
            _fileService = fileService;
        }

        public async Task<MriUploadResponse> CreateAsync(string studentId, MriUploadRequest request, CancellationToken ct = default)
        {
            var storedName = await _fileService.SaveAsync(request.File, "mri", ct);

            var mriCase = new MriCase
            {
                StudentId = studentId,
                StoredFileName = storedName,
                Status = CaseStatus.Uploaded
            };

            await _uow.Repo<MriCase>().AddAsync(mriCase, ct);
            await _uow.SaveChangesAsync(ct);

            return mriCase.Adapt<MriUploadResponse>();
        }

        public async Task<string?> GetStoredFileNameAsync(Guid caseId, CancellationToken ct = default)
        {
            var entity = await _uow.Repo<MriCase>().GetByIdAsync(caseId, ct);
            return entity?.StoredFileName;
        }
        public async Task<PagedResponse<StudentCaseDetailsResponse>>
     GetMyCasesAsync(string studentId, StudentCasesQuery query, CancellationToken ct = default)
        {
            var baseQuery = _uow.Repo<MriCase>()
                .Query()
                .Where(c => c.StudentId == studentId)
                .Include(c => c.AiResult);

            var totalCount = await baseQuery.CountAsync(ct);

            var cases = await baseQuery
                .OrderByDescending(c => c.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(c => new
                {
                    Case = c,
                    Report = _uow.Repo<Report>()
                        .Query()
                        .Where(r => r.CaseId == c.Id && r.StudentId == studentId)
                        .Select(r => new
                        {
                            r.Id,
                            r.SubmittedAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            var items = cases.Select(x => new StudentCaseDetailsResponse
            {
                CaseId = x.Case.Id,
                Status = x.Case.Status,

                IsReportSubmitted =
                    x.Case.Status == CaseStatus.ReportSubmitted ||
                    x.Case.Status == CaseStatus.Predicted ||
                    x.Case.Status == CaseStatus.Reviewed,

                IsPredicted =
                    x.Case.Status == CaseStatus.Predicted ||
                    x.Case.Status == CaseStatus.Reviewed,

                IsReviewed = x.Case.Status == CaseStatus.Reviewed,

                ReportId = x.Report?.Id,
                ReportSubmittedAt = x.Report?.SubmittedAt,

                PredictionResult = x.Case.AiResult?.PredictionResult,
                PredictionCreatedAt = x.Case.AiResult?.CreatedAt,

                CaseCreatedAt = x.Case.CreatedAt,

                ImageUrl = $"/api/Student/MriCases/image/{x.Case.StoredFileName}",

                GradcamUrl = x.Case.AiResult != null
                    ? $"/api/AiTumors/gradcam-image/{x.Case.AiResult.GradcamFileName}"
                    : null

            }).ToList();

            return new PagedResponse<StudentCaseDetailsResponse>
            {
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                Items = items
            };
        }

    }
}
