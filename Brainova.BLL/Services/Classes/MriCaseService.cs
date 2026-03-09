using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Mapster;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

       
    }
}
