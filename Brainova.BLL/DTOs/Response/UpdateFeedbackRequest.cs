using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Brainova.BLL.DTOs.Response
{
    public class UpdateFeedbackRequest
    {
        [Required, MinLength(3), MaxLength(3000)]
        public string Comment { get; set; } = default!;
    }
}
