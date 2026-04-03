using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response.Feedback
{
    public class StudentFeedbacksQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
