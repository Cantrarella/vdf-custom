using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Animation;
using VDF.GUI.Data;
using VDF.GUI.ViewModels;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;
public class InteractionMotionTests {
 static Animation DialogEntryAnimation(IStyle style) {
  if(style is Style candidate && candidate.Selector?.ToString().Contains("Border.dlgframe")==true)
   return candidate.Animations!.OfType<Animation>().Single();
  foreach(var child in style.Children) {
   var result=FindEntry(child);if(result!=null)return result;
  }
  throw new InvalidOperationException("Dialog entry animation not found");
 }
 static Animation? FindEntry(IStyle style) {
  if(style is Style candidate && candidate.Selector?.ToString().Contains("Border.dlgframe")==true)
   return candidate.Animations!.OfType<Animation>().Single();
  foreach(var child in style.Children) {var result=FindEntry(child);if(result!=null)return result;}
  return null;
 }
 static async Task Tick(int milliseconds=35) { await Task.Delay(milliseconds); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); HeadlessUi.Pump(); }
 static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
 [Fact]
 public Task HidingAndReopeningDialogRestoresScrim() => HeadlessUi.Run(async () => {
  var (w,vm)=HeadlessUi.Shell();var before=SettingsFile.Instance.AlwaysReduceMotion;
  var dialog=DialogCatalog.Dialogs["MessageBox"]();
  try {
   SettingsFile.Instance.AlwaysReduceMotion=true;
   dialog.Show();HeadlessUi.Pump();Assert.True(vm.IsDialogDimmed);
   dialog.Hide();HeadlessUi.Pump();Assert.False(vm.IsDialogDimmed);
   Assert.Equal(0,w.FindControl<Border>("DialogScrim")!.Opacity);
   dialog.Show();HeadlessUi.Pump();Assert.True(vm.IsDialogDimmed);
   dialog.Close();HeadlessUi.Pump();Assert.False(vm.IsDialogDimmed);
  } finally {dialog.Close();SettingsFile.Instance.AlwaysReduceMotion=before;await Tick(180);}
 });
 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public Task SwitchKnobHonorsReducedMotion(bool reduce) => HeadlessUi.Run(async () => {
  var before=SettingsFile.Instance.AlwaysReduceMotion;SettingsFile.Instance.AlwaysReduceMotion=reduce;
  var control=new ToggleSwitch { Classes={"plain"} };
  var window=HeadlessUi.Show(control);VDF.GUI.Utils.Appearance.Attach(window);HeadlessUi.Pump();
  try {
   if(reduce) Assert.True(control.KnobTransitions==null || control.KnobTransitions.Count==0);
   else Assert.IsType<DoubleTransition>(Assert.Single(control.KnobTransitions!));
  } finally {window.Close();SettingsFile.Instance.AlwaysReduceMotion=before;await Tick(180);}
 });
 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public Task SidebarHoverInterpolatesUnlessMotionIsReduced(bool reduce) => HeadlessUi.Run(async () => {
  var (w,_) = HeadlessUi.Shell();var before=SettingsFile.Instance.AlwaysReduceMotion;
  var button=w.GetVisualDescendants().OfType<Button>().First(b=>b.Classes.Contains("railitem")&&!b.Classes.Contains("active")&&b.IsEffectivelyVisible);
  var presenter=button.GetVisualDescendants().OfType<ContentPresenter>().First();
  BrushTransition? transition=null;TimeSpan originalDuration=default;
  try {
   ((IPseudoClasses)button.Classes).Set(":pointerover",false);
   SettingsFile.Instance.AlwaysReduceMotion=reduce;HeadlessUi.Pump();await Tick(180);
   if(!reduce) {transition=Assert.IsType<BrushTransition>(Assert.Single(presenter.Transitions!));originalDuration=transition.Duration;Assert.Equal(TimeSpan.FromMilliseconds(120),originalDuration);transition.Duration=TimeSpan.FromSeconds(1);}
   var idle=ColorOf(presenter.Background);
   ((IPseudoClasses)button.Classes).Set(":pointerover",true);HeadlessUi.Pump();
   await Tick();var during=ColorOf(presenter.Background);await Tick(reduce?180:1200);var end=ColorOf(presenter.Background);
   Assert.NotEqual(idle,end);
   if(reduce)Assert.Equal(end,during);else Assert.NotEqual(end,during);
  } finally {if(transition!=null)transition.Duration=originalDuration;((IPseudoClasses)button.Classes).Set(":pointerover",false);SettingsFile.Instance.AlwaysReduceMotion=before;await Tick(180);}
 });
 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public Task ResultsArrowRotatesOnRealHeaderClick(bool reduce) => HeadlessUi.Run(async () => {
  var before=SettingsFile.Instance.AlwaysReduceMotion;
  SettingsFile.Instance.AlwaysReduceMotion=reduce;
  var vm=ResultsFixture.CreatePopulatedViewModel();
  var window=HeadlessUi.Show(new DuplicateResultsView { DataContext=vm });
  VDF.GUI.Utils.Appearance.Attach(window);
  TransformOperationsTransition? transition=null;
  TimeSpan originalDuration=default;
  try {
   await Tick(180);
   Avalonia.Controls.Shapes.Path Arrow() => window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First(p=>p.Classes.Contains("groupcaret"));
   if(!reduce) {
    transition=Assert.IsType<TransformOperationsTransition>(Assert.Single(Arrow().Transitions!));
    originalDuration=transition.Duration;Assert.Equal(TimeSpan.FromMilliseconds(140),originalDuration);
    // Stretch the same transition during this test: rebuilding and rendering the
    // real virtualized list can take longer than 140 ms on a busy test runner.
    transition.Duration=TimeSpan.FromSeconds(2);
   }
   var header=window.GetVisualDescendants().OfType<Border>().First(b=>b.Classes.Contains("cardtop"));
   var position=header.TranslatePoint(new Point(header.Bounds.Width/2,5),window)!.Value;
   window.MouseDown(position,Avalonia.Input.MouseButton.Left);window.MouseUp(position,Avalonia.Input.MouseButton.Left);HeadlessUi.Pump();
   await Tick();var during=Arrow().RenderTransform!.Value;
   await Tick(reduce?180:2200);var end=Arrow().RenderTransform!.Value;
   Assert.True(vm.ResultsRows.OfType<ResultsGroupHeader>().First().IsCollapsed);
   Assert.InRange(end.M12,-.001,.001);
   if(reduce)Assert.Equal(end,during);else Assert.True(during.M12 is >.001 and <.999,$"Group during={during.M12}; classes={string.Join(',',Arrow().Classes)}; transform={Arrow().RenderTransform}");
   // The dedicated button must take the same motion path as the header surface.
   var button=Arrow().FindAncestorOfType<Button>()!;
   position=button.TranslatePoint(new Point(button.Bounds.Width/2,button.Bounds.Height/2),window)!.Value;
   window.MouseDown(position,Avalonia.Input.MouseButton.Left);window.MouseUp(position,Avalonia.Input.MouseButton.Left);HeadlessUi.Pump();
   await Tick();during=Arrow().RenderTransform!.Value;
   await Tick(reduce?180:2200);end=Arrow().RenderTransform!.Value;
   Assert.False(vm.ResultsRows.OfType<ResultsGroupHeader>().First().IsCollapsed);
   Assert.InRange(end.M12,.99,1.01);
   if(reduce)Assert.Equal(end,during);else Assert.InRange(during.M12,.001,.999999);
  } finally {if(transition!=null)transition.Duration=originalDuration;window.Close();SettingsFile.Instance.AlwaysReduceMotion=before;await Tick(180);}
 });
 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public Task SettingsArrowRotatesWithoutResizing(bool reduce) => HeadlessUi.Run(async () => {
  var (w,vm)=HeadlessUi.Shell();var before=SettingsFile.Instance.AlwaysReduceMotion;var expanded=vm.IsRailSettingsExpanded;
  try {
   SettingsFile.Instance.AlwaysReduceMotion=reduce;vm.IsRailSettingsExpanded=false;HeadlessUi.Pump();await Tick(180);
   var arrow=w.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p=>p.Classes.Contains("caret"));
   var size=arrow.Bounds.Size;
   vm.IsRailSettingsExpanded=true;HeadlessUi.Pump();await Tick();
   var during=arrow.RenderTransform!.Value;await Tick(180);var end=arrow.RenderTransform!.Value;
   Assert.Equal(size,arrow.Bounds.Size);Assert.True(end.M12 is >.99 and <1.01,$"Settings end={end.M12}; expanded={vm.IsRailSettingsExpanded}; button={string.Join(',',arrow.FindAncestorOfType<Button>()!.Classes)}");
   if(reduce)Assert.Equal(end,during);else Assert.InRange(during.M12,.001,.999999);
  } finally {vm.IsRailSettingsExpanded=expanded;SettingsFile.Instance.AlwaysReduceMotion=before;await Tick(180);}
 });
 [Fact]
 public Task DialogEntryAndScrimAnimate_AndNestedCloseKeepsDimming() => HeadlessUi.Run(async () => {
  var (w,vm)=HeadlessUi.Shell();var before=SettingsFile.Instance.AlwaysReduceMotion;
  var first=DialogCatalog.Dialogs["MessageBox"]();var second=DialogCatalog.Dialogs["About"]();
  var entry=DialogEntryAnimation(Application.Current!.Styles);var duration=entry.Duration;
  DoubleTransition? fade=null;TimeSpan fadeDuration=default;
  try {
   SettingsFile.Instance.AlwaysReduceMotion=false;HeadlessUi.Pump();await Tick(180);
   // Warm the first layout/font render before measuring a 140 ms animation.
   var warmup=DialogCatalog.Dialogs["MessageBox"]();warmup.Show();HeadlessUi.Pump();await Tick(180);warmup.Close();HeadlessUi.Pump();await Tick(180);
   var scrim=w.FindControl<Border>("DialogScrim")!;
   Assert.Equal(0,scrim.Opacity);
   Assert.Equal(TimeSpan.FromMilliseconds(140),entry.Duration);entry.Duration=TimeSpan.FromSeconds(1);
   fade=Assert.IsType<DoubleTransition>(Assert.Single(scrim.Transitions!));fadeDuration=fade.Duration;
   Assert.Equal(TimeSpan.FromMilliseconds(140),fadeDuration);fade.Duration=TimeSpan.FromSeconds(1);
   var timer=System.Diagnostics.Stopwatch.StartNew();first.Show();HeadlessUi.Pump();await Tick();
   var frame=first.GetVisualDescendants().OfType<Border>().First(b=>b.Classes.Contains("dlgframe"));
   Assert.True(frame.Opacity is >0 and <1,$"Frame={frame.Opacity}, scrim={scrim.Opacity}, elapsed={timer.ElapsedMilliseconds}, classes={string.Join(',',first.Classes)}");
   Assert.InRange(scrim.Opacity,.001,.999);
   Assert.False(scrim.IsHitTestVisible);
   await Tick(1200);Assert.Equal(1,frame.Opacity);Assert.Equal(1,scrim.Opacity);
   second.Show();HeadlessUi.Pump();second.Close();HeadlessUi.Pump();
   Assert.True(vm.IsDialogDimmed);
   first.Close();HeadlessUi.Pump();Assert.False(vm.IsDialogDimmed);
   await Tick(1200);Assert.Equal(0,scrim.Opacity);
  } finally {first.Close();second.Close();entry.Duration=duration;if(fade!=null)fade.Duration=fadeDuration;SettingsFile.Instance.AlwaysReduceMotion=before;await Tick(180);}
 });
 [Fact]
 public Task ReducedMotionShowsDialogAndScrimImmediately() => HeadlessUi.Run(async () => {
  var (w,_)=HeadlessUi.Shell();var before=SettingsFile.Instance.AlwaysReduceMotion;var dialog=DialogCatalog.Dialogs["MessageBox"]();
  try {
   SettingsFile.Instance.AlwaysReduceMotion=true;dialog.Show();HeadlessUi.Pump();
   Assert.Equal(1,dialog.GetVisualDescendants().OfType<Border>().First(b=>b.Classes.Contains("dlgframe")).Opacity);
   Assert.Equal(1,w.FindControl<Border>("DialogScrim")!.Opacity);
   dialog.Close();HeadlessUi.Pump();Assert.Equal(0,w.FindControl<Border>("DialogScrim")!.Opacity);
  } finally {dialog.Close();SettingsFile.Instance.AlwaysReduceMotion=before;await Tick(180);}
 });
}
