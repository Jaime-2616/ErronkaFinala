using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cliente.Services
{
    public interface IAuthService
    {
        Task<bool> ValidateUserAsync(string username, string password);
        Task<bool> IsAdminAsync(string username);
    }
}
