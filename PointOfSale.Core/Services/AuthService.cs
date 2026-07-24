using System.Security.Authentication;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Models.Security;

namespace PointOfSale.Core.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IUserSessionService _userSessionService;

        public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, IUserSessionService userSessionService)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _userSessionService = userSessionService;
        }

        public async Task<User> LoginAsync(string username, string password)
        {
            var user = await _userRepository.GetByUsernameAsync(username);

            // Throw an AuthenticationException for invalid credentials.
            if (user == null || !_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                throw new AuthenticationException("Invalid username or password.");
            }
            var permissions = await _userRepository.GetPermissionsAsync(user.UserId);

            _userSessionService.SetCurrentUser(user);
            _userSessionService.SetPermissions(permissions);

            return user;
        }
    }
}
