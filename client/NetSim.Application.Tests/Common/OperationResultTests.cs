using NetSim.Application.Common;

namespace NetSim.Application.Tests.Common;

public class OperationResultTests
{
    [Fact]
    public void Success_IsSuccessTrue_WithNoError()
    {
        var result = OperationResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_IsSuccessFalse_WithErrorTypeAndMessage()
    {
        var result = OperationResult.Failure(OperationErrorType.NotFound, "not found");

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationErrorType.NotFound, result.ErrorType);
        Assert.Equal("not found", result.ErrorMessage);
    }

    [Fact]
    public void GenericSuccess_CarriesValue()
    {
        var result = OperationResult<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.ErrorType);
    }

    [Fact]
    public void GenericFailure_HasDefaultValue_AndError()
    {
        var result = OperationResult<int>.Failure(OperationErrorType.InvalidState, "bad state");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.Equal(OperationErrorType.InvalidState, result.ErrorType);
        Assert.Equal("bad state", result.ErrorMessage);
    }
}
