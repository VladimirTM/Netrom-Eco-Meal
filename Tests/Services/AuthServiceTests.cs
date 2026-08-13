using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Netrom_Eco_Meal.Entities;
using Netrom_Eco_Meal.Services;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Tests.Services;

// Regression coverage for a real bug found during manual QA: registering with a syntactically
// invalid email used to create a real (unconfirmable, permanently orphaned) ApplicationUser row
// and then crash with an unhandled FormatException the first time anything tried to actually
// parse the address (SmtpEmailSender's confirmation email send). RegisterAsync now rejects it
// up front, before CreateAsync is ever called.
public class AuthServiceTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static SignInManager<ApplicationUser> MockSignInManager(UserManager<ApplicationUser> userManager)
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var options = Options.Create(new IdentityOptions());
        var logger = new Mock<ILogger<SignInManager<ApplicationUser>>>();
        var schemes = new Mock<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        var confirmation = new Mock<IUserConfirmation<ApplicationUser>>();
        return new SignInManager<ApplicationUser>(userManager, contextAccessor.Object, claimsFactory.Object, options, logger.Object, schemes.Object, confirmation.Object);
    }

    private static AuthService Build(out Mock<UserManager<ApplicationUser>> userManager)
    {
        userManager = MockUserManager();
        var signInManager = MockSignInManager(userManager.Object);
        var identityOptions = Options.Create(new IdentityOptions());
        var emailSender = new Mock<IAppEmailSender>();
        var configuration = new ConfigurationBuilder().Build();
        return new AuthService(signInManager, userManager.Object, identityOptions, emailSender.Object, configuration);
    }

    [Fact]
    public async Task RegisterAsync_MalformedEmail_ReturnsFriendlyErrorAndDoesNotCreateUser()
    {
        var service = Build(out var userManager);

        var outcome = await service.RegisterAsync(new RegisterRequest { Email = "not-an-email", Password = "Test1234!" }, "Test User");

        Assert.Equal("Enter a valid email address.", outcome.Error);
        Assert.Null(outcome.Info);
        userManager.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        userManager.Verify(u => u.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ValidEmailAlreadyExists_ReturnsFriendlyErrorAndDoesNotCreateUser()
    {
        var service = Build(out var userManager);
        userManager.Setup(u => u.FindByEmailAsync("existing@example.com")).ReturnsAsync(new ApplicationUser { Name = "Existing", UserName = "existing@example.com", Email = "existing@example.com" });

        var outcome = await service.RegisterAsync(new RegisterRequest { Email = "existing@example.com", Password = "Test1234!" }, "Test User");

        Assert.Equal("An account with this email already exists.", outcome.Error);
        userManager.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }
}
