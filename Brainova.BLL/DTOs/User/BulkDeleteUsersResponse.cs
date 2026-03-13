using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.User
{
    public class BulkDeleteUsersResponse
    {
        public string Message { get; set; }

        public List<DeletedUserResult> Deleted { get; set; } = new();

        public List<FailedUserResult> Failed { get; set; } = new();
    }
}
