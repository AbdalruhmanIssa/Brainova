using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.DAL.Modles
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } // we set it in SaveChanges if empty
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
