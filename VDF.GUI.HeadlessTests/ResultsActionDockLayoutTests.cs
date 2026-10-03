using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

public class ResultsActionDockLayoutTests {
    [Theory]
    [InlineData("en", 500)]
    [InlineData("zh-Hans", 500)]
    [InlineData("en", 340)]
    [InlineData("zh-Hans", 340)]
    public Task SelectedFileActions_RemainVisibleAtNarrowWidths(string language, double width) => HeadlessUi.Run(() => {
        App.Lang.LoadLanguage(language);
        var vm = ResultsFixture.CreatePopulatedViewModel();
        vm.Duplicates.First().Checked = true;
        var view = new DuplicateResultsView { DataContext = vm };
        var window = HeadlessUi.Show(view, width, 1000);
        try {
            var dock = view.FindControl<Border>("ResultsActionDock")!;
            Assert.True(dock.IsEffectivelyVisible);
            foreach (var button in dock.GetVisualDescendants().OfType<Button>()) {
                var point = button.TranslatePoint(default, dock)!.Value;
                Assert.True(point.X >= 0 && point.X + button.Bounds.Width <= dock.Bounds.Width + 0.5,
                    $"{language} {width}: action extends past the dock");
                Assert.True(button.Bounds.Width + 0.5 >= button.DesiredSize.Width - button.Margin.Left - button.Margin.Right);
            }
        }
        finally { window.Close(); App.Lang.LoadLanguage("en"); HeadlessUi.Pump(); }
    });
}
