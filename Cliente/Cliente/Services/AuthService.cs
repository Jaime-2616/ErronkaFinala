using System;
using System.Threading.Tasks;

namespace Cliente.Services
{
    public class AuthService : IAuthService
    {
        public async Task<bool> ValidateUserAsync(string username, string password)
        {
            // Adaptado a tu ServerService actual
            string response = ServerService.SendRequest("login", username, password);
            // Si quieres hacerlo 100% async, puedes crear un wrapper async,
            // pero para el ejercicio esto vale.
            await Task.CompletedTask;

            return !string.IsNullOrEmpty(response) &&
                   response.StartsWith("OK|", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> IsAdminAsync(string username)
        {
            // Para el ejercicio: "admin" es admin, el resto, usuario normal
            await Task.CompletedTask;
            return string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
