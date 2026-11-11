using System.Linq.Expressions;
using Brainova.BLL.DTOs.Request;
using Brainova.BLL.DTOs.Response.Report;
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
    /// ReportService.SubmitAsync is the longest method in the project: about
    /// ten checks, then two saves, then a notification. Each check gets its
    /// own test, in the same order the service runs them.
    ///
    /// NEW CONCEPT 1 — the transaction wrapper.
    /// SubmitAsync runs its work inside _uow.ExecuteInTransactionAsync(...).
    /// A mock would normally just return null without running the work, so
    /// the constructor below tells the mock: "when you are given an
    /// operation, run it and return its result".
    ///
    /// NEW CONCEPT 2 — faking a database-generated value.
    /// ReportNumber is produced by SQL Server on the first SaveChanges. There
    /// is no database here, so SaveChangesAsync gets a Callback that sets it,
    /// the way the database would.
    /// </summary>
    public class ReportServiceTests
    {
        private const string StudentId = "stu-1";
        private const string SupervisorId = "sup-1";

        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<INotificationService> _notifications = new();
        private readonly ReportService _sut;

        public ReportServiceTests()
        {
            _sut = new ReportService(_uow.Object, _notifications.Object);

            // Run the transactional operation instead of skipping it.
            _uow.Setup(u => u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task<(Report, ApplicationUser)>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Func<Task<(Report, ApplicationUser)>> operation, CancellationToken _) => operation());
        }

        // =====================================================================
        // SubmitAsync — guard clauses, in the order the service checks them
        // =====================================================================

        [Fact]
        public async Task SubmitAsync_WhenCaseDoesNotExist_ThrowsNotFound()
        {
            var s = Arrange(caseExists: false);

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Case not found");
        }

        [Fact]
        public async Task SubmitAsync_WhenCaseBelongsToAnotherStudent_ThrowsForbidden()
        {
            var s = Arrange(caseOwnerId: "someone-else");

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<ForbiddenException>().WithMessage("You don't own this case");
        }

        [Theory]
        [InlineData(CaseStatus.ReportSubmitted)]
        [InlineData(CaseStatus.Predicted)]
        [InlineData(CaseStatus.Reviewed)]
        public async Task SubmitAsync_WhenCaseIsNotInUploadedStatus_ThrowsBadRequest(CaseStatus status)
        {
            var s = Arrange(caseStatus: status);

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Report already submitted or case closed");
        }

        [Fact]
        public async Task SubmitAsync_WhenReportAlreadyExists_ThrowsBadRequest()
        {
            var s = Arrange(reportAlreadyExists: true);

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Report already exists for this case");
        }

        [Fact]
        public async Task SubmitAsync_WhenStudentDoesNotExist_ThrowsNotFound()
        {
            var s = Arrange(studentExists: false);

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Student not found");
        }

        [Fact]
        public async Task SubmitAsync_WhenStudentHasNoSupervisor_ThrowsBadRequest()
        {
            var s = Arrange(studentSupervisorId: null);

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Student has no assigned supervisor");
        }

        [Fact]
        public async Task SubmitAsync_WhenSupervisorHasNoActiveQuestions_ThrowsBadRequest()
        {
            var s = Arrange(questionsAreActive: false);

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("No active report questions found");
        }

        [Fact]
        public async Task SubmitAsync_WhenAnAnswerTargetsAnUnknownQuestion_ThrowsBadRequest()
        {
            var s = Arrange();
            var request = Request(s,
                (s.Preliminary.Id, "Tumor"),
                (Guid.NewGuid(), "answer to a question that is not in the form"));

            var act = () => _sut.SubmitAsync(StudentId, request);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("One or more submitted question ids are invalid");
        }

        [Fact]
        public async Task SubmitAsync_WhenPreliminaryAssessmentQuestionIsMissing_ThrowsBadRequest()
        {
            var s = Arrange(includePreliminaryQuestion: false);
            var request = Request(s, (s.Details.Id, "Left temporal lobe"));

            var act = () => _sut.SubmitAsync(StudentId, request);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Preliminary assessment question is missing");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SubmitAsync_WhenPreliminaryAnswerIsBlank_ThrowsBadRequest(string? answer)
        {
            var s = Arrange();
            var request = Request(s, (s.Preliminary.Id, answer), (s.Details.Id, "Left temporal lobe"));

            var act = () => _sut.SubmitAsync(StudentId, request);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Preliminary assessment answer is required");
        }

        // The preliminary question is single-choice: only its options are allowed.
        [Fact]
        public async Task SubmitAsync_WhenPreliminaryAnswerIsNotOneOfTheOptions_ThrowsBadRequest()
        {
            var s = Arrange();
            var request = Request(s, (s.Preliminary.Id, "banana"), (s.Details.Id, "Left temporal lobe"));

            var act = () => _sut.SubmitAsync(StudentId, request);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Invalid answer 'banana' for 'Preliminary Assesment'*");
        }

        [Fact]
        public async Task SubmitAsync_WhenARequiredAnswerIsMissing_ThrowsBadRequest()
        {
            var s = Arrange();
            var request = Request(s, (s.Preliminary.Id, "Tumor"));   // "Details" is required but absent

            var act = () => _sut.SubmitAsync(StudentId, request);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Answer for 'Details' is required.");
        }

        [Fact]
        public async Task SubmitAsync_WhenDatabaseDoesNotGenerateAReportNumber_ThrowsBadRequest()
        {
            var s = Arrange(generatedReportNumber: 0);

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Failed to generate report number.");
        }

        // =====================================================================
        // SubmitAsync — happy paths
        // =====================================================================

        [Fact]
        public async Task SubmitAsync_HappyPath_SavesReportAndAnswers_AndMarksCaseSubmitted()
        {
            var s = Arrange(generatedReportNumber: 42);

            var reportId = await _sut.SubmitAsync(StudentId, ValidRequest(s));

            // The report that was added is the one whose id is returned.
            s.AddedReport.Should().NotBeNull();
            reportId.Should().Be(s.AddedReport!.Id);
            s.AddedReport.CaseId.Should().Be(s.Case.Id);
            s.AddedReport.StudentId.Should().Be(StudentId);

            // Report code = REP-{year}-{number padded to 6 digits}
            s.AddedReport.ReportCode.Should().Be($"REP-{s.AddedReport.SubmittedAt.Year}-000042");

            // One answer row per active question, with a snapshot of the question text.
            s.SavedAnswers.Should().HaveCount(2);
            s.SavedAnswers.Should().OnlyContain(a => a.ReportId == reportId);
            s.SavedAnswers.Single(a => a.QuestionId == s.Details.Id)
                .QuestionTextSnapshot.Should().Be(s.Details.Text);

            // Case status moved on, and both saves happened.
            s.Case.Status.Should().Be(CaseStatus.ReportSubmitted);
            s.CaseRepo.Verify(r => r.Update(s.Case), Times.Once);
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task SubmitAsync_TrimsAnswers_AndAcceptsChoiceInAnyLetterCase()
        {
            var s = Arrange();
            var request = Request(s, (s.Preliminary.Id, "  tUmOr "), (s.Details.Id, "  Left temporal lobe  "));

            await _sut.SubmitAsync(StudentId, request);

            s.SavedAnswers.Single(a => a.QuestionId == s.Preliminary.Id).AnswerValue.Should().Be("tUmOr");
            s.SavedAnswers.Single(a => a.QuestionId == s.Details.Id).AnswerValue.Should().Be("Left temporal lobe");
        }

        // "No tumor" → questions flagged SkipWhenNoTumor are stored with a
        // null answer, even though they are required and even if the student
        // typed something.
        [Fact]
        public async Task SubmitAsync_WhenAnswerIsNoTumor_SkipsQuestionsMarkedSkipWhenNoTumor()
        {
            var s = Arrange();
            var request = Request(s, (s.Preliminary.Id, "No Tumor"), (s.Details.Id, "should be ignored"));

            await _sut.SubmitAsync(StudentId, request);

            s.SavedAnswers.Should().HaveCount(2);
            s.SavedAnswers.Single(a => a.QuestionId == s.Details.Id).AnswerValue.Should().BeNull();
            s.SavedAnswers.Single(a => a.QuestionId == s.Preliminary.Id).AnswerValue.Should().Be("No Tumor");
        }

        [Fact]
        public async Task SubmitAsync_NotifiesTheStudentsSupervisor()
        {
            var s = Arrange();

            var reportId = await _sut.SubmitAsync(StudentId, ValidRequest(s));

            _notifications.Verify(n => n.NotifySupervisorNewReportAsync(
                SupervisorId,
                It.Is<SupervisorNewReportResponse>(p => p.ReportId == reportId
                                                     && p.CaseId == s.Case.Id
                                                     && p.StudentId == StudentId
                                                     && p.StudentName == "Student One"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // Same promise as FeedbackServiceTests Level 5: a SignalR failure must
        // not fail a request whose data is already saved.
        [Fact]
        public async Task SubmitAsync_WhenNotificationThrows_StillReturnsTheReportId()
        {
            var s = Arrange();
            _notifications
                .Setup(n => n.NotifySupervisorNewReportAsync(
                    It.IsAny<string>(), It.IsAny<SupervisorNewReportResponse>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("SignalR is down"));

            var reportId = await _sut.SubmitAsync(StudentId, ValidRequest(s));

            reportId.Should().Be(s.AddedReport!.Id);
        }

        [Fact]
        public async Task SubmitAsync_WhenAGuardFails_NothingIsSavedAndNobodyIsNotified()
        {
            var s = Arrange(caseStatus: CaseStatus.Reviewed);

            var act = () => _sut.SubmitAsync(StudentId, ValidRequest(s));

            await act.Should().ThrowAsync<BadRequestException>();
            s.AddedReport.Should().BeNull();
            s.SavedAnswers.Should().BeEmpty();
            _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            _notifications.VerifyNoOtherCalls();
        }

        // =====================================================================
        // GetNewForSupervisorAsync
        // =====================================================================

        [Fact]
        public async Task GetNewForSupervisorAsync_ReturnsUnreviewedReportsOfOwnStudents_NewestFirst()
        {
            var t0 = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

            var older = MakeReport(SupervisorId, CaseStatus.ReportSubmitted, submittedAt: t0);
            var newer = MakeReport(SupervisorId, CaseStatus.Predicted, submittedAt: t0.AddDays(1));
            var alreadyReviewed = MakeReport(SupervisorId, CaseStatus.Reviewed, submittedAt: t0.AddDays(2));
            var otherSupervisors = MakeReport("sup-2", CaseStatus.ReportSubmitted, submittedAt: t0.AddDays(3));

            _uow.SetupRepo(new List<Report> { older, newer, alreadyReviewed, otherSupervisors });

            var result = await _sut.GetNewForSupervisorAsync(SupervisorId);

            result.Select(r => r.ReportId).Should().Equal(newer.Id, older.Id);
            result[0].StudentName.Should().Be("Student One");
        }

        // =====================================================================
        // GetSupervisorDetailsAsync
        // =====================================================================

        [Fact]
        public async Task GetSupervisorDetailsAsync_WhenReportDoesNotExist_ThrowsNotFound()
        {
            _uow.SetupRepo(new List<Report>());

            var act = () => _sut.GetSupervisorDetailsAsync(SupervisorId, Guid.NewGuid());

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Report not found");
        }

        [Fact]
        public async Task GetSupervisorDetailsAsync_WhenStudentBelongsToAnotherSupervisor_ThrowsBadRequest()
        {
            var report = MakeReport("sup-2", CaseStatus.Predicted);
            _uow.SetupRepo(new List<Report> { report });

            var act = () => _sut.GetSupervisorDetailsAsync(SupervisorId, report.Id);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Not your student");
        }

        [Fact]
        public async Task GetSupervisorDetailsAsync_ReturnsAnswersInQuestionOrder_AndLabelledProbabilities()
        {
            var report = MakeReport(SupervisorId, CaseStatus.Predicted, probabilitiesJson: "[0.7,0.1,0.15,0.05]");
            _uow.SetupRepo(new List<Report> { report });
            _uow.SetupRepo(new List<ReportAnswer>
            {
                MakeAnswer(report.Id, code: "SECOND", order: 2, value: "b"),
                MakeAnswer(report.Id, code: "FIRST", order: 1, value: "a"),
                MakeAnswer(Guid.NewGuid(), code: "OTHER-REPORT", order: 0, value: "x")
            });

            var result = await _sut.GetSupervisorDetailsAsync(SupervisorId, report.Id);

            result.ReportId.Should().Be(report.Id);
            result.StudentName.Should().Be("Student One");
            result.PredictionResult.Should().Be("glioma");
            result.StoredFileName.Should().Be("scan.jpg");

            result.Answers.Select(a => a.Code).Should().Equal("FIRST", "SECOND");

            // The JSON array is positional: [Glioma, Meningioma, No Tumor, Pituitary]
            result.Probabilities.Select(p => p.Label)
                  .Should().Equal("Glioma", "Meningioma", "No Tumor", "Pituitary");
            result.Probabilities.Select(p => p.Value)
                  .Should().Equal(0.7f, 0.1f, 0.15f, 0.05f);
        }

        [Theory]
        [InlineData("this is not json")]
        [InlineData("[0.5,0.5]")]          // fewer than 4 values
        [InlineData("")]
        public async Task GetSupervisorDetailsAsync_WhenProbabilitiesJsonIsUnusable_ReturnsEmptyProbabilities(string json)
        {
            var report = MakeReport(SupervisorId, CaseStatus.Predicted, probabilitiesJson: json);
            _uow.SetupRepo(new List<Report> { report });
            _uow.SetupRepo(new List<ReportAnswer>());

            var result = await _sut.GetSupervisorDetailsAsync(SupervisorId, report.Id);

            result.Probabilities.Should().BeEmpty();
        }

        // =====================================================================
        // PDF details (used by ReportPdfService)
        // =====================================================================

        [Fact]
        public async Task GetSupervisorPdfDetailsAsync_WhenReportDoesNotExist_ThrowsNotFound()
        {
            _uow.SetupRepo(new List<Report>());

            var act = () => _sut.GetSupervisorPdfDetailsAsync(SupervisorId, Guid.NewGuid());

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Report not found");
        }

        [Fact]
        public async Task GetSupervisorPdfDetailsAsync_WhenStudentBelongsToAnotherSupervisor_ThrowsBadRequest()
        {
            var report = MakeReport("sup-2", CaseStatus.Predicted);
            _uow.SetupRepo(new List<Report> { report });

            var act = () => _sut.GetSupervisorPdfDetailsAsync(SupervisorId, report.Id);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Not your student");
        }

        [Fact]
        public async Task GetStudentPdfDetailsAsync_WhenReportDoesNotExist_ThrowsNotFound()
        {
            _uow.SetupRepo(new List<Report>());

            var act = () => _sut.GetStudentPdfDetailsAsync(StudentId, Guid.NewGuid());

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Report not found");
        }

        [Fact]
        public async Task GetStudentPdfDetailsAsync_WhenReportBelongsToAnotherStudent_ThrowsBadRequest()
        {
            var report = MakeReport(SupervisorId, CaseStatus.Predicted);
            _uow.SetupRepo(new List<Report> { report });

            var act = () => _sut.GetStudentPdfDetailsAsync("another-student", report.Id);

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("This report does not belong to you");
        }

        [Fact]
        public async Task GetStudentPdfDetailsAsync_ReturnsReportWithSupervisorNameAndAnswers()
        {
            var report = MakeReport(SupervisorId, CaseStatus.Reviewed, probabilitiesJson: "[0.7,0.1,0.15,0.05]");
            _uow.SetupRepo(new List<Report> { report });
            _uow.SetupRepo(new List<ReportAnswer> { MakeAnswer(report.Id, code: "Q1", order: 1, value: "a") });
            _uow.SetupRepo(new List<ApplicationUser>
            {
                new() { Id = SupervisorId, FullName = "Dr. Supervisor" }
            });

            var result = await _sut.GetStudentPdfDetailsAsync(StudentId, report.Id);

            result.ReportId.Should().Be(report.Id);
            result.StudentName.Should().Be("Student One");
            result.SupervisorName.Should().Be("Dr. Supervisor");
            result.PredictionResult.Should().Be("glioma");
            result.Probabilities.Should().HaveCount(4);
            result.Answers.Should().ContainSingle().Which.Code.Should().Be("Q1");
        }

        [Fact]
        public async Task GetSupervisorPdfDetailsAsync_WhenSupervisorUserIsGone_UsesNotAvailableAsName()
        {
            var report = MakeReport(SupervisorId, CaseStatus.Reviewed);
            _uow.SetupRepo(new List<Report> { report });
            _uow.SetupRepo(new List<ReportAnswer>());
            _uow.SetupRepo(new List<ApplicationUser>());   // supervisor row not found

            var result = await _sut.GetSupervisorPdfDetailsAsync(SupervisorId, report.Id);

            result.SupervisorName.Should().Be("N/A");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Everything one SubmitAsync test needs, built by Arrange().</summary>
        private sealed class Scenario
        {
            public MriCase Case = default!;
            public ReportQuestion Preliminary = default!;
            public ReportQuestion Details = default!;
            public Mock<IGenericRepository<MriCase>> CaseRepo = default!;

            /// <summary>The Report the service passed to reportRepo.AddAsync (null if it never got that far).</summary>
            public Report? AddedReport;

            /// <summary>Every ReportAnswer the service passed to answerRepo.AddAsync.</summary>
            public List<ReportAnswer> SavedAnswers = new();
        }

        /// <summary>
        /// Builds a fully valid submission scenario. Each optional parameter
        /// breaks exactly ONE thing, so a guard-clause test reads as
        /// "everything is fine except …".
        /// </summary>
        private Scenario Arrange(
            bool caseExists = true,
            string caseOwnerId = StudentId,
            CaseStatus caseStatus = CaseStatus.Uploaded,
            bool reportAlreadyExists = false,
            bool studentExists = true,
            string? studentSupervisorId = SupervisorId,
            bool questionsAreActive = true,
            bool includePreliminaryQuestion = true,
            long generatedReportNumber = 1)
        {
            var s = new Scenario
            {
                Case = new MriCase
                {
                    Id = Guid.NewGuid(),
                    StudentId = caseOwnerId,
                    StoredFileName = "scan.jpg",
                    Status = caseStatus
                },
                // The service finds this question by its code (note the
                // spelling "Assesment" — it must match the seeded data).
                Preliminary = new ReportQuestion
                {
                    Id = Guid.NewGuid(),
                    Code = "Preliminary Assesment",
                    Text = "Is there a tumor?",
                    Type = ReportQuestionType.SingleChoice,
                    OptionsJson = "[\"Tumor\",\"No Tumor\"]",
                    Order = 1,
                    IsActive = questionsAreActive,
                    IsRequired = true,
                    SupervisorId = SupervisorId
                },
                Details = new ReportQuestion
                {
                    Id = Guid.NewGuid(),
                    Code = "Details",
                    Text = "Describe the tumor location.",
                    Type = ReportQuestionType.Text,
                    Order = 2,
                    IsActive = questionsAreActive,
                    IsRequired = true,
                    SkipWhenNoTumor = true,
                    SupervisorId = SupervisorId
                }
            };

            // --- MriCase repo: GetByIdAsync returns the case (or null) ---
            s.CaseRepo = _uow.SetupRepo(new List<MriCase> { s.Case });
            s.CaseRepo.Setup(r => r.GetByIdAsync(s.Case.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(caseExists ? s.Case : null);

            // --- Report repo: duplicate check + capture the added report ---
            var reportRepo = _uow.SetupRepo(new List<Report>());
            reportRepo.Setup(r => r.ExistsAsync(
                    It.IsAny<Expression<Func<Report, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(reportAlreadyExists);
            reportRepo.Setup(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
                .Callback((Report report, CancellationToken _) => s.AddedReport = report)
                .Returns(Task.CompletedTask);

            // --- "The database": the first save generates the ReportNumber ---
            _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    if (s.AddedReport != null && s.AddedReport.ReportNumber == 0)
                        s.AddedReport.ReportNumber = generatedReportNumber;
                })
                .ReturnsAsync(1);

            // --- Student (with or without a supervisor) ---
            var users = new List<ApplicationUser>();
            if (studentExists)
            {
                users.Add(new ApplicationUser
                {
                    Id = StudentId,
                    FullName = "Student One",
                    SupervisorUserId = studentSupervisorId
                });
            }
            _uow.SetupRepo(users);

            // --- The supervisor's question form ---
            var questions = new List<ReportQuestion> { s.Details };
            if (includePreliminaryQuestion)
                questions.Add(s.Preliminary);
            _uow.SetupRepo(questions);

            // --- Answer repo: remember every answer that gets added ---
            var answerRepo = _uow.SetupRepo(new List<ReportAnswer>());
            answerRepo.Setup(r => r.AddAsync(It.IsAny<ReportAnswer>(), It.IsAny<CancellationToken>()))
                .Callback((ReportAnswer answer, CancellationToken _) => s.SavedAnswers.Add(answer))
                .Returns(Task.CompletedTask);

            return s;
        }

        /// <summary>A request that answers both questions correctly.</summary>
        private static SubmitReportRequest ValidRequest(Scenario s)
            => Request(s, (s.Preliminary.Id, "Tumor"), (s.Details.Id, "Left temporal lobe"));

        private static SubmitReportRequest Request(Scenario s, params (Guid QuestionId, string? Value)[] answers)
            => new()
            {
                CaseId = s.Case.Id,
                Answers = answers
                    .Select(a => new SubmitReportAnswerItem { QuestionId = a.QuestionId, AnswerValue = a.Value! })
                    .ToList()
            };

        /// <summary>A saved report whose student belongs to the given supervisor.</summary>
        private static Report MakeReport(
            string supervisorUserId,
            CaseStatus caseStatus,
            DateTime? submittedAt = null,
            string probabilitiesJson = "[0.7,0.1,0.15,0.05]")
        {
            var created = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

            var student = new ApplicationUser
            {
                Id = StudentId,
                FullName = "Student One",
                Email = "student.one@brainova.test",
                SupervisorUserId = supervisorUserId
            };

            var mriCase = new MriCase
            {
                Id = Guid.NewGuid(),
                StudentId = StudentId,
                Student = student,
                StoredFileName = "scan.jpg",
                Status = caseStatus,
                CreatedAt = created
            };
            mriCase.AiResult = new AiResult
            {
                CaseId = mriCase.Id,
                PredictionResult = "glioma",
                ProbabilitiesJson = probabilitiesJson,
                GradcamFileName = "cam.jpg",
                CreatedAt = created.AddMinutes(10)
            };

            return new Report
            {
                Id = Guid.NewGuid(),
                ReportCode = "REP-2026-000001",
                CaseId = mriCase.Id,
                Case = mriCase,
                StudentId = StudentId,
                Student = student,
                SubmittedAt = submittedAt ?? created.AddHours(1)
            };
        }

        private static ReportAnswer MakeAnswer(Guid reportId, string code, int order, string value)
            => new()
            {
                Id = Guid.NewGuid(),
                ReportId = reportId,
                QuestionId = Guid.NewGuid(),
                AnswerValue = value,
                Question = new ReportQuestion
                {
                    Code = code,
                    Text = "Text of " + code,
                    Type = ReportQuestionType.Text,
                    Order = order
                }
            };
    }
}
