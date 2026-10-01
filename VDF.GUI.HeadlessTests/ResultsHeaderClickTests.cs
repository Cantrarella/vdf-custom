using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using VDF.GUI.ViewModels;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;
public class ResultsHeaderClickTests {
 [Fact]
 public Task HeaderSurfaceToggles_ButActionButtonsDoNot() => HeadlessUi.Run(() => {
  var vm = ResultsFixture.CreatePopulatedViewModel();
  var window = HeadlessUi.Show(new DuplicateResultsView { DataContext = vm });
  try {
   Border Header() => window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("cardtop"));
   void Click(Control control, Point local) {
    var point = control.TranslatePoint(local, window)!.Value;
    window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); HeadlessUi.Pump();
   }
   var header = Header(); var group = (ResultsGroupHeader)header.DataContext!;
   Click(header, new Point(header.Bounds.Width / 2, 5));
   Assert.True(vm.ResultsRows.OfType<ResultsGroupHeader>().First().IsCollapsed);
   header = Header();
   var title = header.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == group.Title);
   Click(title, new Point(3,3));
   Assert.False(vm.ResultsRows.OfType<ResultsGroupHeader>().First().IsCollapsed);
   var arrow = Header().GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("chevron"));
   Click(arrow, new Point(arrow.Bounds.Width/2,arrow.Bounds.Height/2));
   Assert.True(vm.ResultsRows.OfType<ResultsGroupHeader>().First().IsCollapsed);
   var keep = Header().GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, App.Lang["Results.Group.KeepBest"]));
   Click(keep, new Point(keep.Bounds.Width/2,keep.Bounds.Height/2));
   Assert.True(vm.ResultsRows.OfType<ResultsGroupHeader>().First().IsCollapsed);
  } finally { window.Close(); }
 });
}
