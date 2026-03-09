using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.DAL.Utilites
{
    public interface ISeedData
    {
        Task DataSeedingAsync();
        Task IdentitySeedingAsync();
    }
}
