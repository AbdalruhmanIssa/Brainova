using System;
using System.Collections.Generic;

namespace Brainova.DAL.Modles
{
    public class Report : BaseEntity
    {
        public Guid CaseId { get; set; }
        public MriCase Case { get; set; } = default!;

        public string StudentId { get; set; } = default!;
        public ApplicationUser Student { get; set; } = default!;

        public DateTime SubmittedAt { get; set; }

        public ICollection<ReportAnswer> Answers { get; set; } = new List<ReportAnswer>();
    }
}