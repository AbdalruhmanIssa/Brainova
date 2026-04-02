using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brainova.DAL.Modles
{
        public class Feedback : BaseEntity
        {
            public Guid ReportId { get; set; }
            public Report Report { get; set; } = default!;

            public string SupervisorId { get; set; } = default!;
            public ApplicationUser Supervisor { get; set; } = default!;

            public string StudentId { get; set; } = default!;
            public ApplicationUser Student { get; set; } = default!;

            public string Comment { get; set; } = default!;

        public bool IsSeen { get; set; } = false;

    }
}
