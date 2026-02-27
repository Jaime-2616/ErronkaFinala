using System;
using System.Threading.Tasks;
using Cliente.Services;
using Cliente.ViewModels;
using Moq;
using Xunit;

namespace Tests
{
    public class LoginViewModelTests
    {
        [Fact]
        public async Task Login_Fails_When_Empty_Username_Or_Password()
        {
            var authMock = new Mock<IAuthService>();
            var vm = new LoginViewModel(authMock.Object);

            vm.Username = "";
            vm.Password = "";

            bool result = await vm.LoginAsync();

            Assert.False(result);
            Assert.False(vm.IsAuthenticated);
            Assert.Contains("vacíos", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            authMock.Verify(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Login_Fails_When_Invalid_Credentials()
        {
            var authMock = new Mock<IAuthService>();
            authMock
                .Setup(x => x.ValidateUserAsync("user", "bad"))
                .ReturnsAsync(false);

            var vm = new LoginViewModel(authMock.Object)
            {
                Username = "user",
                Password = "bad"
            };

            bool result = await vm.LoginAsync();

            Assert.False(result);
            Assert.False(vm.IsAuthenticated);
            Assert.False(vm.IsAdmin);
            Assert.Contains("Credenciales incorrectas", vm.ErrorMessage);

            authMock.Verify(x => x.ValidateUserAsync("user", "bad"), Times.Once);
            authMock.Verify(x => x.IsAdminAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Login_Succeeds_For_Normal_User()
        {
            var authMock = new Mock<IAuthService>();
            authMock
                .Setup(x => x.ValidateUserAsync("user", "1234"))
                .ReturnsAsync(true);
            authMock
                .Setup(x => x.IsAdminAsync("user"))
                .ReturnsAsync(false);

            var vm = new LoginViewModel(authMock.Object)
            {
                Username = "user",
                Password = "1234"
            };

            bool result = await vm.LoginAsync();

            Assert.True(result);
            Assert.True(vm.IsAuthenticated);
            Assert.False(vm.IsAdmin);
            Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
        }

        [Fact]
        public async Task Login_Succeeds_For_Admin_User()
        {
            var authMock = new Mock<IAuthService>();
            authMock
                .Setup(x => x.ValidateUserAsync("admin", "admin1234"))
                .ReturnsAsync(true);
            authMock
                .Setup(x => x.IsAdminAsync("admin"))
                .ReturnsAsync(true);

            var vm = new LoginViewModel(authMock.Object)
            {
                Username = "admin",
                Password = "admin1234"
            };

            bool result = await vm.LoginAsync();

            Assert.True(result);
            Assert.True(vm.IsAuthenticated);
            Assert.True(vm.IsAdmin);
            Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
        }

        [Fact]
        public async Task Login_Fails_On_Connection_Exception()
        {
            var authMock = new Mock<IAuthService>();
            authMock
                .Setup(x => x.ValidateUserAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Network error"));

            var vm = new LoginViewModel(authMock.Object)
            {
                Username = "user",
                Password = "1234"
            };

            bool result = await vm.LoginAsync();

            Assert.False(result);
            Assert.False(vm.IsAuthenticated);
            Assert.Contains("Konexio errorea", vm.ErrorMessage);
        }
    }
}