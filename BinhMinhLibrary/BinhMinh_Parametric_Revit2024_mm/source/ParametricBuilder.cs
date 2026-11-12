using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
namespace BinhMinhParametric {
 public static partial class Builder {
  static string root;
  static double M(double mm){return mm/304.8;}
  static void Log(string s){File.AppendAllText(Path.Combine(root,"parametric.log"),DateTime.Now.ToString("HH:mm:ss")+" "+s+Environment.NewLine);}
  static FamilyParameter P(Document d,string name,double mm){var p=d.FamilyManager.AddParameter(name,GroupTypeId.Geometry,SpecTypeId.Length,false);d.FamilyManager.Set(p,M(mm));return p;}
  public static void Execute(UIApplication ui,string folder){root=folder;if(File.Exists(Path.Combine(root,"parametric.verify"))){System.Reflection.Assembly.LoadFrom(Path.Combine(root,"BinhMinh.Parametric2024.dll"));if(File.Exists(Path.Combine(root,"parametric.finish")))Finish(ui);else Verify(ui);}else BuildAll(ui);}
 }
}
