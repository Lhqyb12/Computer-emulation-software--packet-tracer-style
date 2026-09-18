using NetSim.Server.Services;  // PasswordPolicy - exactly what these tests are checking

namespace NetSim.Server.Tests;

// The testing framework here is xUnit - the most common one for modern .NET testing (also referenced in the project's csproj)
public class PasswordPolicyTests
{
    // [Fact] marks a "plain" test - a single, fixed scenario, runs exactly once
    [Fact]
    public void A_strong_password_is_accepted()
    {
        // The method name itself (A_strong_password_is_accepted) is part of the documentation - when a test
        // fails, the method name shows up in the report and immediately explains *what* was supposed to
        // happen, without needing to open the function body
        // 8+ chars, has letters, a digit, a special character, no username inside
        string? error = PasswordPolicy.Validate("Secret1!", "lea");

        // Assert.Null checks that the result is exactly null - i.e. that the function "approved" the password,
        // per the convention we set in PasswordPolicy (null = valid)
        Assert.Null(error);
    }

    // [Theory] + [InlineData]: a "parameterized" test - the same method runs once separately for each
    // InlineData row, with a different password each time. This avoids duplicating nearly identical test methods 5 times
    [Theory]
    [InlineData("Ab1!")]          // too short
    [InlineData("1234567!")]      // no letter
    [InlineData("Abcdefg!")]      // no number
    [InlineData("Abcdefg1")]      // no special character
    [InlineData("")]              // empty
    public void A_weak_password_is_rejected_with_a_message(string password)
    {
        string? error = PasswordPolicy.Validate(password, "tester");

        // Checks two things: that an error was returned at all (NotNull), and also that it isn't an empty
        // string (NotEqual) - i.e. it's not enough to have "something non-null", it must be a message with actual content the user can read
        Assert.NotNull(error);
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void The_message_says_what_is_missing()
    {
        // This test checks not just *that* there is an error, but that the message's *content* is relevant to
        // the specific problem - Assert.Contains checks that a substring (e.g. "number") appears inside the
        // full error message.
        // "!" (the null-forgiving operator) after Validate(...) tells the compiler "I know this isn't null
        // here, don't warn about CS8602" - because in the context of this test we're certain these calls must
        // return an error (not null)
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
        // Checks not just that it was rejected, but that it was rejected *for the right reason* (the message
        // contains "username") - to make sure it's the username check that failed, and not, coincidentally, some other check (like length)
        Assert.Contains("username", error!);
    }

    [Fact]
    public void Username_match_is_case_insensitive()
    {
        // Explicitly tests the OrdinalIgnoreCase choice made in the source code: "LEA" in uppercase inside the
        // password is still detected as containing the username "Lea" (with different casing) - a security
        // rule that still works even if a user tries to "get around" it just by changing letter casing
        Assert.NotNull(PasswordPolicy.Validate("xxLEAxx9!", "Lea"));
    }

    [Fact]
    public void Very_short_username_is_not_used_for_this_check()
    {
        // Explicitly tests the "trimmedUser.Length >= 3" condition in the source code: a username shorter than
        // 3 characters ("al") should not reject a password that just *happens* to contain those same 2
        // characters - this is the tradeoff test described above
        // username "al" (2 chars) must not ban every password containing "al"
        Assert.Null(PasswordPolicy.Validate("Normal1!", "al"));
    }
}
