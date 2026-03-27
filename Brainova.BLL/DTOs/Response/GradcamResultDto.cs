using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Brainova.BLL.DTOs.Response
{
    public class GradcamResultDto
    {
      
        public string Label { get; set; } = "";
        public float[] Probabilities { get; set; } = Array.Empty<float>();
        [JsonIgnore]
        public string FileName { get; set; } = "";  
        public string GradcamUrl { get; set; } = ""; 
    }
}

