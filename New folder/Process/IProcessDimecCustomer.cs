using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentAssignment.Process
{
    public interface IProcessDimecCustomer
    {
        void ReviewAndProcessDimec(IConfiguration configuration);
    }
}
