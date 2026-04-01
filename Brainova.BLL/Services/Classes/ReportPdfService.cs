using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Enums;
using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace Brainova.BLL.Services.Classes
{
    public class ReportPdfService : IReportPdfService
    {
        private readonly IReportService _reportService;
        private readonly IWebHostEnvironment _env;

        public ReportPdfService(IReportService reportService, IWebHostEnvironment env)
        {
            _reportService = reportService;
            _env = env;
        }

        public async Task<byte[]> GenerateSupervisorReportPdfAsync(string supervisorId, Guid reportId, CancellationToken ct = default)
        {
            var model = await _reportService.GetSupervisorPdfDetailsAsync(supervisorId, reportId);
            return await BuildPdfAsync(model, ct);
        }

        public async Task<byte[]> GenerateStudentReportPdfAsync(string studentId, Guid reportId, CancellationToken ct = default)
        {
            var model = await _reportService.GetStudentPdfDetailsAsync(studentId, reportId);
            return await BuildPdfAsync(model, ct);
        }

        private async Task<byte[]> BuildPdfAsync(ReportPdfResponse model, CancellationToken ct = default)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var imagePath = Path.Combine(_env.ContentRootPath, "App_Data", "mri", model.StoredFileName);
            byte[]? imageBytes = null;

            if (File.Exists(imagePath))
                imageBytes = await File.ReadAllBytesAsync(imagePath, ct);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(c => ComposeHeader(c, model));
                    page.Content().Element(c => ComposeContent(c, model, imageBytes));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, ReportPdfResponse model)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("BRAINOVA").Bold().FontSize(22).FontColor(Colors.Blue.Darken2);
                        c.Item().Text("Brain Tumor Detection Report").FontSize(12).SemiBold();
                        c.Item().Text("Palestine Technical University – Kadoorie").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                });

                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Blue.Medium);
            });
        }

        private void ComposeContent(IContainer container, ReportPdfResponse model, byte[]? imageBytes)
        {
            container.Column(col =>
            {
                col.Spacing(14);

                col.Item().Element(c => ComposeInfoSection(c, model));
                col.Item().Element(c => ComposeImageSection(c, imageBytes));
                col.Item().Element(c => ComposeAnswersSection(c, model));
                col.Item().Element(c => ComposeAiSection(c, model));
            });
        }

        private void ComposeInfoSection(IContainer container, ReportPdfResponse model)
        {
            container.Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.Grey.Lighten5)
                .Padding(12)
                .Column(col =>
                {
                    col.Spacing(6);

                    col.Item().Text("Report Information").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Student Name: {model.StudentName}").SemiBold();
                            c.Item().Text($"Supervisor Name: {model.SupervisorName}");
                            c.Item().Text($"Submitted At: {FormatDate(model.SubmittedAt)}");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Case Created: {FormatNullableDate(model.CaseCreatedAt)}");
                            c.Item().Text($"Prediction Created: {FormatNullableDate(model.PredictionCreatedAt)}");
                            c.Item().Text($"Case ID: {model.CaseId}");
                        });
                    });
                });
        }

        private void ComposeImageSection(IContainer container, byte[]? imageBytes)
        {
            container.Column(col =>
            {
                col.Item().Text("MRI Image").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);

                col.Item()
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .Padding(10)
                    .AlignCenter()
                    .Height(260)
                    .Element(c =>
                    {
                        if (imageBytes != null)
                            c.Image(imageBytes, ImageScaling.FitArea);
                        else
                            c.AlignCenter().AlignMiddle().Text("MRI image not found").FontColor(Colors.Red.Darken1);
                    });
            });
        }

        private void ComposeAnswersSection(IContainer container, ReportPdfResponse model)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("Student Answers").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);

                if (model.Answers == null || model.Answers.Count == 0)
                {
                    col.Item().Text("No answers available.");
                    return;
                }

                int index = 1;
                foreach (var answer in model.Answers)
                {
                    col.Item()
                        .Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(10)
                        .Column(c =>
                        {
                            c.Spacing(4);
                            c.Item().Text($"{index}. {answer.Question}").Bold();
                            c.Item().Text($"Answer: {FormatAnswer(answer)}");
                        });

                    index++;
                }
            });
        }

        private void ComposeAiSection(IContainer container, ReportPdfResponse model)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("AI Prediction Summary").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);

                col.Item()
                    .Background(Colors.Blue.Lighten5)
                    .Border(1)
                    .BorderColor(Colors.Blue.Lighten2)
                    .Padding(12)
                    .Column(c =>
                    {
                        c.Spacing(6);
                        c.Item().Text($"Predicted Result: {model.PredictionResult ?? "Not Available"}")
                            .Bold()
                            .FontSize(13);

                        var top = model.Probabilities?.OrderByDescending(x => x.Value).FirstOrDefault();
                        if (top != null)
                            c.Item().Text($"Highest Probability: {top.Label} ({top.Value:P2})").SemiBold();

                        if (model.Probabilities != null && model.Probabilities.Count > 0)
                        {
                            foreach (var item in model.Probabilities.OrderByDescending(x => x.Value))
                            {
                                c.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(item.Label);
                                    row.ConstantItem(90).AlignRight().Text($"{item.Value:P2}");
                                });
                            }
                        }
                        else
                        {
                            c.Item().Text("Probabilities are not available.");
                        }
                    });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.PaddingTop(6).Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                col.Item().PaddingTop(5).AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1));
                    text.Span("Generated by Brainova Medical Training Platform • ");
                    text.Span("All rights reserved").SemiBold();
                });
            });
        }

        private static string FormatDate(DateTime value)
            => value.ToLocalTime().ToString("dd MMM yyyy - hh:mm tt");

        private static string FormatNullableDate(DateTime? value)
            => value.HasValue ? value.Value.ToLocalTime().ToString("dd MMM yyyy - hh:mm tt") : "N/A";

        private static string FormatAnswer(ReportPdfAnswerResponse answer)
        {
            if (string.IsNullOrWhiteSpace(answer.AnswerValue))
                return "No answer";

            return answer.AnswerValue;
        }
    }
}