using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace DotnetDoctor.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void German_resources_cover_the_default_keys_and_format_placeholders()
    {
        var english = ReadResources(CultureInfo.InvariantCulture);
        var german = ReadResources(CultureInfo.GetCultureInfo("de"));

        Assert.NotEmpty(english);
        Assert.Equal(english.Keys.Order(), german.Keys.Order());
        foreach (var (key, value) in english)
        {
            var expected = Regex.Matches(value, @"\{\d+\}").Select(match => match.Value).Order();
            var actual = Regex.Matches(german[key], @"\{\d+\}").Select(match => match.Value).Order();
            Assert.Equal(expected, actual);
        }
    }

    private static Dictionary<string, string> ReadResources(CultureInfo culture)
    {
        var resources = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(resources);
        return resources.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }
}
