using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.DAL.Modles
{
    public class AiResult : BaseEntity
    {
        public Guid CaseId { get; set; } // FK -> MriCases.Id (Unique)
        public MriCase MriCase { get; set; } = default!;

        public string PredictionResult { get; set; } = default!;

        // store probabilities as JSON string (easy + flexible)
        public string ProbabilitiesJson { get; set; } = default!;

        // gradcam saved on disk like you already do
        public string GradcamFileName { get; set; } = default!;

       
    }
}
