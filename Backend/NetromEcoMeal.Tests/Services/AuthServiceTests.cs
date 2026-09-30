using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using NetromEcoMeal.Entities;
using NetromEcoMeal.Services;
using NetromEcoMeal.Services.Interfaces;
using NetromEcoMeal.Tests.TestSupport;

namespace NetromEcoMeal.Tests.Services;

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

    private static AuthService Build(out Mock<UserManager<ApplicationUser>> userManager)
    {
        userManager = MockUserManager();
        var identityOptions = Options.Create(new IdentityOptions());
        var emailSender = new Mock<IAppEmailSender>();
        var configuration = new ConfigurationBuilder().Build();
        var currentUser = new FakeCurrentUser(null);
        var referralService = new Mock<IReferralService>();
        return new AuthService(userManager.Object, identityOptions, emailSender.Object, configuration, referralService.Object, currentUser);
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

    // Regression coverage: RegisterAsync no longer signs the
    // user in itself (that needs a real HTTP response, which only AuthController has) — it now
    // hands the created user back via RegisterOutcome.UserToSignIn for the caller to sign in.
    [Fact]
    public async Task RegisterAsync_Success_ReturnsCreatedUserToSignIn()
    {
        var service = Build(out var userManager);
        userManager.Setup(u => u.FindByEmailAsync("new@example.com")).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var outcome = await service.RegisterAsync(new RegisterRequest { Email = "new@example.com", Password = "Test1234!" }, "New User");

        Assert.Null(outcome.Error);
        Assert.Null(outcome.Info);
        Assert.NotNull(outcome.UserToSignIn);
        Assert.Equal("new@example.com", outcome.UserToSignIn!.Email);
    }
}
