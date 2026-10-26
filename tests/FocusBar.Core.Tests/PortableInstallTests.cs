using FocusBar.Core.Updates;

namespace FocusBar.Core.Tests;

public class PortableInstallTests
{
    [Fact]
    public void Nothing_installed_means_install() =>
        Assert.Equal(InstallAction.Install, PortableInstall.Decide(new Version(0, 9, 1), null));

    [Theory]
    [InlineData("0.9.0", "0.9.1")]
    [InlineData("0.9.1.0", "0.9.1")] // same version: still replace (a rebuilt test copy)
    [InlineData("0.8", "0.9.1")]
    public void Older_or_same_installed_means_update(string installed, string download) =>
        Assert.Equal(InstallAction.Update, PortableInstall.Decide(Version.Parse(download), Version.Parse(installed)));

    [Theory]
    [InlineData("1.0.0", "0.9.1")]
    [InlineData("0.9.2", "0.9.1.5")]
    public void Newer_installed_is_not_replaced_without_asking(string installed, string download) =>
        Assert.Equal(InstallAction.InstalledIsNewer, PortableInstall.Decide(Version.Parse(download), Version.Parse(installed)));

    [Theory]
    [InlineData(@"C:\Users\Sam\AppData\Local\Programs\FocusBar", @"c:\users\sam\appdata\local\programs\focusbar\", true)]
    [InlineData(@"C:\Users\Sam\AppData\Local\Programs\FocusBar", @"C:\Users\Sam\Downloads\FocusBar-0.9.1-portable", false)]
    [InlineData(@"C:\Apps\FocusBar\", @"C:\Apps\FocusBar", true)]
    public void Recognizes_the_same_folder(string a, string b, bool same) => Assert.Equal(same, PortableInstall.SameFolder(a, b));

    [Fact]
    public void Copies_everything_but_the_marker()
    {
        var files = PortableInstall.FilesToCopy(
        [
            "FocusBar.exe", "FocusBar.portable", "README-FIRST.txt", @"runtimes\win-x64\native\WebView2Loader.dll", "",
            "FocusBar.Blocker.exe",
        ]);

        Assert.Equal(["FocusBar.Blocker.exe", "FocusBar.exe", "README-FIRST.txt", @"runtimes\win-x64\native\WebView2Loader.dll"], files);
    }
}
