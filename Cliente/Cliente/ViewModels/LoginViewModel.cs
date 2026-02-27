using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cliente.Services;

namespace Cliente.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;

        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isAuthenticated;
        private bool _isAdmin;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            private set { _isAuthenticated = value; OnPropertyChanged(); }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            private set { _isAdmin = value; OnPropertyChanged(); }
        }

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<bool> LoginAsync()
        {
            ErrorMessage = string.Empty;
            IsAuthenticated = false;
            IsAdmin = false;

            // 3.a - sarrera hutsik
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Nombre de usuario o contraseña vacíos.";
                return false;
            }

            try
            {
                // 3.b / 3.c / 3.d - lógica de validación
                bool ok = await _authService.ValidateUserAsync(Username, Password);
                if (!ok)
                {
                    ErrorMessage = "Credenciales incorrectas.";
                    return false;
                }

                bool isAdmin = await _authService.IsAdminAsync(Username);

                IsAuthenticated = true;
                IsAdmin = isAdmin;
                ErrorMessage = string.Empty;
                return true;
            }
            catch (InvalidOperationException)
            {
                // 3.e - konexio salbuespena
                ErrorMessage = "Konexio errorea";
                return false;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Errore ezezaguna: " + ex.Message;
                return false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
