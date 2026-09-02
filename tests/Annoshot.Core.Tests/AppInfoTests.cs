using Xunit;

namespace Annoshot.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsAnnoshot()
    {
        Assert.Equal("annoshot", AppInfo.Name);
    }
}
