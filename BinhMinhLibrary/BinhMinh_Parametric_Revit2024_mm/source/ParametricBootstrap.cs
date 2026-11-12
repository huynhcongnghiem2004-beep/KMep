using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
namespace BinhMinhParametric {
 public sealed class Bootstrap:IExternalApplication {
  static readonly string Root=Path.GetDirectoryName(typeof(Bootstrap).Assembly.Location);
  public Result OnStartup(UIControlledApplication app){app.Idling+=Run;return Result.Succeeded;}
  public Result OnShutdown(UIControlledApplication app){app.Idling-=Run;return Result.Succeeded;}
  void Run(object sender,IdlingEventArgs args){
   string request=Path.Combine(Root,"parametric.request");if(!File.Exists(request))return;
   File.Move(request,Path.Combine(Root,"parametric.running."+DateTime.Now.ToString("yyyyMMddHHmmssfff")));
   try{var asm=Assembly.Load(File.ReadAllBytes(Path.Combine(Root,"ParametricBuilder.dll")));asm.GetType("BinhMinhParametric.Builder").GetMethod("Execute").Invoke(null,new object[]{(UIApplication)sender,Root});}
   catch(Exception ex){File.AppendAllText(Path.Combine(Root,"parametric.log"),DateTime.Now+" FATAL "+ex+Environment.NewLine);}
  }
 }
}
