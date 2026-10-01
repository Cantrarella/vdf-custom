using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace VDF.GUI.HeadlessTests;
public class BrandBackgroundTests {
 [Theory]
 [InlineData("Light")]
 [InlineData("Dark")]
 public Task BrandContent_HasNoStateFill(string theme) => HeadlessUi.Run(() => {
  var (w,_) = HeadlessUi.Shell();
  var before=w.RequestedThemeVariant;
  var button=w.FindControl<Button>("RailBrandCapsule")!;
  try {
   w.RequestedThemeVariant=theme=="Light"?ThemeVariant.Light:ThemeVariant.Dark;
   foreach(var state in new[]{":pointerover",":pressed",":focus"}) {
    ((IPseudoClasses)button.Classes).Set(state,true);HeadlessUi.Pump();
    foreach(var presenter in button.GetVisualDescendants().OfType<ContentPresenter>())
     Assert.True(presenter.Background is null || presenter.Background is ISolidColorBrush b && b.Color.A==0,"Brand content must not paint a white or gray state tile.");
    ((IPseudoClasses)button.Classes).Set(state,false);
   }
  } finally {w.RequestedThemeVariant=before;HeadlessUi.Pump();}
 });
}
