using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.User
{
    public class FailedUserResult
    {
        public string Id { get; set; }
        public string? UserName { get; set; }
        public string Reason { get; set; }
    }
}