using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Brainova.BLL.DTOs.Request
{
    public class UpdateFeedbackRequest
    {
        public string Comment { get; set; } = null!;
    }
}