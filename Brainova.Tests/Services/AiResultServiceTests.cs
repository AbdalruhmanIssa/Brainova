using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// AiResultService.PredictAsync = load the case → refuse duplicates →
    /// read the MRI image from disk → call the AI service → save the result.
    ///
    /// The AI service (IAiTumorService) is mocked, so no Python/HTTP call is
    /// made. The image is a real file in a temp folder, because the service
    /// uses File.Exists / FileStream directly.
    /// </summary>
    public class AiResultServiceTests : IDisposable
    {
        private const string StudentId = "stu-1";

        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<IAiTumorService> _ai = new();
        private readonly TempContentRoot _root = new();
        private readonly AiResultService _sut;

        public AiResultServiceTests()
        {
            _sut = new AiResultService(_uow.Object, _root.Env.Object, _ai.Object);
        }

        public void Dispose() => _root.Dispose();

        [Fact]
        public async Task PredictAsync_WhenCaseDoesNotExist_ThrowsNotFound()
        {
            _uow.SetupRepo(new List<MriCase>());
            _uow.SetupRepo(new List<AiResult>());

            var act = () => _sut.PredictAsync(StudentId, Guid.NewGuid());

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Case not found.");
        }

        // Ownership check: another student's case must look like "not found",
        // so a student cannot even learn that the case id exists.
        [Fact]
        public async Task PredictAsync_WhenCaseBelongsToAnotherStudent_ThrowsNotFound()
        {
            var mriCase = MakeCase(studentId: "someone-else");
            _uow.SetupRepo(new List<MriCase> { mriCase });
            _uow.SetupRepo(new List<AiResult>());

            var act = () => _sut.PredictAsync(StudentId, mriCase.Id);

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Case not found.");
        }

        [Fact]
        public async Task PredictAsync_WhenPredictionAlreadyExists_ThrowsBadRequest_AndDoesNotCallAi()
        {
            var mriCase = MakeCase();
            _uow.SetupRepo(new List<MriCase> { mriCase });
            _uow.SetupRepo(new List<AiResult> { new() { CaseId = mriCase.Id } });

            var act = () => _sut.PredictAsync(StudentId, mriCase.Id);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("AI prediction already exists for this case.");
            VerifyAiNeverCalled();
        }

        [Theory]
        [InlineData(CaseStatus.Predicted)]
        [InlineData(CaseStatus.Reviewed)]
        public async Task PredictAsync_WhenCaseStatusIsAlreadyPredicted_ThrowsBadRequest(CaseStatus status)
        {
            var mriCase = MakeCase(status: status);
            _uow.SetupRepo(new List<MriCase> { mriCase });
            _uow.SetupRepo(new List<AiResult>());

            var act = () => _sut.PredictAsync(StudentId, mriCase.Id);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("This case has already been predicted.");
            VerifyAiNeverCalled();
        }

        [Fact]
        public async Task PredictAsync_WhenImageFileIsMissingOnDisk_ThrowsNotFound()
        {
            var mriCase = MakeCase();   // file is NOT written to the temp folder
            _uow.SetupRepo(new List<MriCase> { mriCase });
            _uow.SetupRepo(new List<AiResult>());

            var act = () => _sut.PredictAsync(StudentId, mriCase.Id);

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Image file is missing.");
            VerifyAiNeverCalled();
        }

        [Fact]
        public async Task PredictAsync_HappyPath_SavesResult_MarksCasePredicted_AndReturnsResponse()
        {
            // Arrange: a case whose image really exists on disk…
            var mriCase = MakeCase();
            _root.WriteAppDataFile("mri", mriCase.StoredFileName, new byte[] { 1, 2, 3 });

            var caseRepo = _uow.SetupRepo(new List<MriCase> { mriCase });
            var aiRepo = _uow.SetupRepo(new List<AiResult>());

            // …and an AI service that answers "glioma".
            var probabilities = new[] { 0.9f, 0.05f, 0.03f, 0.02f };
            _ai.Setup(a => a.GetGradcamAsync(
                    It.IsAny<Stream>(), mriCase.StoredFileName, "image/jpeg", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new GradcamResultDto
               {
                   Label = "glioma",
                   Probabilities = probabilities,
                   FileName = "cam.jpg"
               });

            // Act
            var result = await _sut.PredictAsync(StudentId, mriCase.Id);

            // Assert — response
            result.CaseId.Should().Be(mriCase.Id);
            result.Prediction.Should().Be("glioma");
            result.Probabilities.Should().Equal(probabilities);
            result.GradcamUrl.Should().Be("/api/AiTumors/gradcam-image/cam.jpg");

            // Assert — an AiResult row was added for this case
            aiRepo.Verify(r => r.AddAsync(
                It.Is<AiResult>(x => x.CaseId == mriCase.Id
                                  && x.PredictionResult == "glioma"
                                  && x.GradcamFileName == "cam.jpg"
                                  && x.ProbabilitiesJson.StartsWith("[")),
                It.IsAny<CancellationToken>()), Times.Once);

            // Assert — the case moved to Predicted and everything was saved once
            mriCase.Status.Should().Be(CaseStatus.Predicted);
            caseRepo.Verify(r => r.Update(mriCase), Times.Once);
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // If the AI call fails, nothing may be written: no AiResult row, and
        // the case must stay "Uploaded" so the student can retry.
        [Fact]
        public async Task PredictAsync_WhenAiServiceThrows_SavesNothing()
        {
            var mriCase = MakeCase();
            _root.WriteAppDataFile("mri", mriCase.StoredFileName, new byte[] { 1, 2, 3 });

            _uow.SetupRepo(new List<MriCase> { mriCase });
            var aiRepo = _uow.SetupRepo(new List<AiResult>());

            _ai.Setup(a => a.GetGradcamAsync(
                    It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new BadRequestException("Not a brain MRI"));

            var act = () => _sut.PredictAsync(StudentId, mriCase.Id);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Not a brain MRI");
            mriCase.Status.Should().Be(CaseStatus.Uploaded);
            aiRepo.Verify(r => r.AddAsync(It.IsAny<AiResult>(), It.IsAny<CancellationToken>()), Times.Never);
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static MriCase MakeCase(string studentId = StudentId, CaseStatus status = CaseStatus.Uploaded)
            => new()
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                StoredFileName = "scan.jpg",
                Status = status
            };

        private void VerifyAiNeverCalled()
            => _ai.Verify(a => a.GetGradcamAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }
}
