using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Response.Feedback
{
    public class StudentNotificationsResult
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int UnseenCount { get; set; }
        public List<StudentFeedbackNotificationResponse> Items { get; set; } = new();
    }
}
