using Brainova.DAL.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.DAL.Modles
{
    public class MriCase : BaseEntity
    {
        public string StudentId { get; set; } = default!; // FK -> ApplicationUser.Id
        public ApplicationUser Student { get; set; } = default!;

        // Store relative path OR stored file name (recommended)
        public string StoredFileName { get; set; } = default!; // e.g. "App_Data/mri/xxx.jpg"


        public CaseStatus Status { get; set; } = CaseStatus.Uploaded;

        // navigation (0/1)
         public AiResult? AiResult { get; set; }
        //  public Report? Report { get; set; }
    }

}
