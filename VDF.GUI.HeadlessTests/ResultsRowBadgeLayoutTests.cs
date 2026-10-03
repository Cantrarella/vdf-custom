using Avalonia.Controls;
using Avalonia.VisualTree;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

public class ResultsRowBadgeLayoutTests {
    [Theory]
    [InlineData("en", 500)]
    [InlineData("zh-Hans", 500)]
    [InlineData("zh-Hans", 650)]
    public Task StatusBadges_DoNotOverlapComparisonMetadata(string language, double width) => HeadlessUi.Run(() => {
        App.Lang.LoadLanguage(language);
        var view = new DuplicateResultsView { DataContext = ResultsFixture.CreatePopulatedViewModel() };
        var window = HeadlessUi.Show(view, width, 1000);
        try {
            var badgeRows = view.GetVisualDescendants().OfType<WrapPanel>().Where(p => p.Classes.Contains("rowbadges")).ToList();
            Assert.NotEmpty(badgeRows);
            foreach (var badges in badgeRows) {
                var info = (StackPanel)badges.Parent!;
                var metadata = info.Children.OfType<WrapPanel>().Single(p => p.Classes.Contains("rowmetadata"));
                Assert.True(badges.Bounds.Bottom <= metadata.Bounds.Y, "Badges overlap comparison values");
                foreach (var badge in badges.Children.Where(c => c.IsVisible)) {
                    Assert.True(badge.Bounds.Right <= badges.Bounds.Width + 0.5, "Badge extends past available row width");
                }
                foreach (var metric in metadata.Children.Where(c => c.IsVisible)) {
                    Assert.True(metric.Bounds.Right <= metadata.Bounds.Width + 0.5, "Comparison values extend past available row width");
                }
            }
        }
        finally { window.Close(); App.Lang.LoadLanguage("en"); HeadlessUi.Pump(); }
    });
}
