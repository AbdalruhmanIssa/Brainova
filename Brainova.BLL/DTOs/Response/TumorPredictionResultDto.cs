using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response
{
    public class TumorPredictionResultDto
    {
        public string Prediction { get; set; } = string.Empty;

        public float GliomaProbability { get; set; }
        public float MeningiomaProbability { get; set; }
        public float NoTumorProbability { get; set; }
        public float PituitaryProbability { get; set; }
    }
}
