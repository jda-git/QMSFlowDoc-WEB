using QMSFlowDoc.Shared.Validation;

namespace QMSFlowDoc.Tests.Compliance;

public class SetupPasswordPolicyTests
{
    [Fact]
    public void AdminSetupPasswordPolicy_AcceptsIdentityCompliantPassword()
    {
        var result = PasswordPolicy.Validate("Admin123!");

        Assert.True(result.IsValid, result.ErrorMessage);
    }

    [Theory]
    [InlineData("Admin123")]
    [InlineData("ADMIN123!")]
    public void AdminSetupPasswordPolicy_RejectsPasswordsIdentityWouldReject(string password)
    {
        var result = PasswordPolicy.Validate(password);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }
}
