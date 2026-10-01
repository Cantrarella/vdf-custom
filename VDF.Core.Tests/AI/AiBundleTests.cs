using VDF.Core.AI;

namespace VDF.Core.Tests.AI;
public class AiBundleTests {
 [Theory]
 [InlineData(true, true, true, true)]
 [InlineData(false, true, true, false)]
 [InlineData(true, false, true, false)]
 [InlineData(true, true, false, false)]
 public void CompleteBundle_UsesApplicationFolder_OtherwiseUsesWritableState(bool model, bool runtime, bool version, bool bundled) {
  string root=Path.Combine(Path.GetTempPath(), "VDF.AiBundleTests."+Guid.NewGuid().ToString("N"));
  string app=Path.Combine(root,"application"),state=Path.Combine(root,"state"),ai=Path.Combine(app,"ai");
  Directory.CreateDirectory(ai);
  try {
   if(model) File.WriteAllBytes(Path.Combine(ai,AiComponents.ModelFileName),[1]);
   if(runtime) File.WriteAllBytes(Path.Combine(ai,"onnxruntime.dll"),[1]);
   File.WriteAllText(Path.Combine(ai,"runtime.version"),version?AiComponents.RuntimeVersion:"old");
   Assert.Equal(bundled?ai:Path.Combine(state,"ai"),AiComponents.ResolveAiFolder(state,app));
  } finally {Directory.Delete(root,true);}
 }
}
