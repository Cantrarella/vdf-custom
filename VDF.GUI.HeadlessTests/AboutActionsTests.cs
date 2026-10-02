using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using VDF.GUI.Data;
using VDF.GUI.Utils;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

public class AboutActionsTests {
 [Fact]
 public Task AllFourButtonsPerformTheirActions() => HeadlessUi.Run(async () => {
  HeadlessUi.Shell();
  var dialog=new AboutWindow();
  var links=new List<string>();
  dialog.UrlOpener=links.Add;
  try {
   dialog.Show();HeadlessUi.Pump();await Task.Delay(180);
   void Click(string name) {
    var button=dialog.FindControl<Button>(name)!;
    Assert.True(button.IsEnabled);
    var point=button.TranslatePoint(new Point(button.Bounds.Width/2,button.Bounds.Height/2),dialog)!.Value;
    dialog.MouseDown(point,MouseButton.Left);dialog.MouseUp(point,MouseButton.Left);HeadlessUi.Pump();
   }
   Click("ProjectPageButton");
   Click("LatestReleaseButton");
   Assert.Equal(new[]{"https://github.com/0x90d/videoduplicatefinder","https://github.com/0x90d/videoduplicatefinder/releases"},links);
   Assert.NotNull(dialog.Clipboard);
   await dialog.Clipboard!.SetTextAsync("before copy");
   Click("CopyButton");await Task.Delay(50);HeadlessUi.Pump();
   var text=await dialog.Clipboard.TryGetValueAsync(DataFormat.Text);
   Assert.Contains("Video Duplicate Finder",text);
   Assert.Contains(VersionInfo.LongDisplay,text);
   Assert.Contains(".NET",text);
   Click("OkButton");Assert.False(dialog.IsVisible);
  } finally {dialog.Close();}
 });
}
