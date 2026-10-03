using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

public class ResultsResponsiveLayoutTests {
    [Theory]
    [InlineData("zh-Hans", 500, 1.0)]
    [InlineData("en", 500, 1.0)]
    [InlineData("zh-Hans", 750, 1.5)]
    [InlineData("en", 750, 1.5)]
    [InlineData("zh-Hans", 900, 1.0)]
    public Task ToolbarAndFilters_StayWithinTheAvailableWidth(string language, double width, double scale) => HeadlessUi.Run(() => {
        App.Lang.LoadLanguage(language);
        var vm = ResultsFixture.CreatePopulatedViewModel();
        vm.ResultsAdvancedFiltersOpen = true;
        var view = new DuplicateResultsView { DataContext = vm };
        var host = new LayoutTransformControl { Child = view, LayoutTransform = new ScaleTransform(scale, scale) };
        var window = HeadlessUi.Show(host, width, 1000);
        try {
            var search = view.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("searchbox"));
            var toolbar = search.GetVisualAncestors().OfType<Border>().First();
            var advanced = view.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("advstrip"));
            foreach (var control in toolbar.GetVisualDescendants().Concat(advanced.GetVisualDescendants()).OfType<Control>()
                .Where(c => c.IsEffectivelyVisible && c is Button or ToggleButton or ComboBox or NumericUpDown or CheckBox or Slider)) {
                var point = control.TranslatePoint(default, view)!.Value;
                Assert.True(point.X >= -0.5 && point.X + control.Bounds.Width <= view.Bounds.Width + 0.5,
                    $"{language} {width}/{scale}: {control.GetType().Name} {control.Name} extends past the view: {point.X}+{control.Bounds.Width}>{view.Bounds.Width}");
                Assert.True(control.Bounds.Width + 0.5 >= control.DesiredSize.Width - control.Margin.Left - control.Margin.Right,
                    $"{control.GetType().Name} {control.Name} is horizontally clipped");
            }
            if (Environment.GetEnvironmentVariable("VDF_LAYOUT_SCREENSHOTS") is { } output && language == "zh-Hans" && scale == 1) {
                Directory.CreateDirectory(output);
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark }) {
                    window.RequestedThemeVariant = theme;
                    HeadlessUi.Pump();
                    using var frame = window.CaptureRenderedFrame();
                    frame!.Save(Path.Combine(output, $"filters-{width}-{theme}.png"));
                }
            }
        }
        finally { window.Close(); App.Lang.LoadLanguage("en"); HeadlessUi.Pump(); }
    });

    [Fact]
    public Task ExpandedFilters_LeaveRoomForResultsInAShortWindow() => HeadlessUi.Run(() => {
        var vm = ResultsFixture.CreatePopulatedViewModel();
        vm.ResultsAdvancedFiltersOpen = true;
        var view = new DuplicateResultsView { DataContext = vm };
        var window = HeadlessUi.Show(view, 600, 600);
        try {
            Assert.True(view.FindControl<ListBox>("ResultsList")!.Bounds.Height >= 100,
                $"expanded filters consume the results viewport: list={view.FindControl<ListBox>("ResultsList")!.Bounds.Height}, view={view.Bounds.Height}, max={view.FindControl<ScrollViewer>("AdvancedFiltersScroller")!.MaxHeight}, advanced={view.FindControl<ScrollViewer>("AdvancedFiltersScroller")!.Bounds.Height}");
        }
        finally { window.Close(); HeadlessUi.Pump(); }
    });
}
