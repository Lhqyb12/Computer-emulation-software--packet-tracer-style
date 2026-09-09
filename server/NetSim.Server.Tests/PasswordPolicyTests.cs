using NetSim.Server.Services;

namespace NetSim.Server.Tests;

public class PasswordPolicyTests
{
    [Fact]
    public void A_strong_password_is_accepted()
    {
        // 8+ chars, has letters, a digit, a special character, no username inside
        string? error = PasswordPolicy.Validate("Secret1!", "lea");

        Assert.Null(error);
    }

    [Theory]
    [InlineData("Ab1!")]          // too short
    [InlineData("1234567!")]      // no letter
    [InlineData("Abcdefg!")]      // no number
    [InlineData("Abcdefg1")]      // no special character
    [InlineData("")]              // empty
    public void A_weak_password_is_rejected_with_a_message(string password)
    {
        string? error = PasswordPolicy.Validate(password, "tester");

        Assert.NotNull(error);
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void The_message_says_what_is_missing()
    {
        Assert.Contains("number", PasswordPolicy.Validate("Abcdefg!", "tester")!);
        Assert.Contains("letter", PasswordPolicy.Validate("1234567!", "tester")!);
        Assert.Contains("special", PasswordPolicy.Validate("Abcdefg1", "tester")!);
        Assert.Contains("8", PasswordPolicy.Validate("Ab1!", "tester")!);
    }

    [Fact]
    public void Password_containing_the_username_is_rejected()
    {
        // otherwise strong, but contains "lea"
        string? error = PasswordPolicy.Validate("MyLea123!", "lea");

        Assert.NotNull(error);
        Assert.Contains("username", error!);
    }

    [Fact]
    public void Username_match_is_case_insensitive()
    {
        Assert.NotNull(PasswordPolicy.Validate("xxLEAxx9!", "Lea"));
    }

    [Fact]
    public void Very_short_username_is_not_used_for_this_check()
    {
        // username "al" (2 chars) must not ban every password containing "al"
        Assert.Null(PasswordPolicy.Validate("Normal1!", "al"));
    }
}
