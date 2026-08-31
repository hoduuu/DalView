using DalView.ViewModels;
using Xunit;

namespace DalView.Tests;

public class SearchNavigatorTests
{
    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(2, 3, 0)]
    public void Next_WrapsAround(int current, int count, int expected)
    {
        Assert.Equal(expected, SearchNavigator.Next(current, count));
    }

    [Theory]
    [InlineData(1, 3, 0)]
    [InlineData(0, 3, 2)]
    public void Previous_WrapsAround(int current, int count, int expected)
    {
        Assert.Equal(expected, SearchNavigator.Previous(current, count));
    }

    [Fact]
    public void Next_ReturnsNegativeOne_WhenNoMatches()
    {
        Assert.Equal(-1, SearchNavigator.Next(0, 0));
    }
}
