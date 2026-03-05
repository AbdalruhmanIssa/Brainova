using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response
{
    public class PredictResponse
    {
        public Guid CaseId { get; set; }
        public string Prediction { get; set; } = default!;
        public float[] Probabilities { get; set; } = Array.Empty<float>();
        public string GradcamUrl { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}
