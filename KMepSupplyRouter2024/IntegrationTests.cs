using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
namespace KMep.Supply {
 public static class IntegrationTests {
  sealed class Case {public string Name,Series="PVC",ExpectedError;public double DN=21,MainDN=34;public XYZ U=-XYZ.BasisZ,T=XYZ.BasisZ;public bool Hot,Endpoint,Obstacle,ConnectedMain,Blocked,WrongSystem,Fixture,Toilet,Linked,NearEnd,Flex,DirectTee,ShortEnd,Guided,Shaft,Neighbours;public bool Expected=true;}
  static XYZ P(double x,double y,double z){return new XYZ(x/304.8,y/304.8,z/304.8);}
  static void Block(Document d,XYZ a,XYZ b){var loop=new CurveLoop();loop.Append(Line.CreateBound(new XYZ(a.X,a.Y,a.Z),new XYZ(b.X,a.Y,a.Z)));loop.Append(Line.CreateBound(new XYZ(b.X,a.Y,a.Z),new XYZ(b.X,b.Y,a.Z)));loop.Append(Line.CreateBound(new XYZ(b.X,b.Y,a.Z),new XYZ(a.X,b.Y,a.Z)));loop.Append(Line.CreateBound(new XYZ(a.X,b.Y,a.Z),new XYZ(a.X,a.Y,a.Z)));var solid=GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{loop},XYZ.BasisZ,b.Z-a.Z);var ds=DirectShape.CreateElement(d,new ElementId(BuiltInCategory.OST_GenericModel));ds.SetShape(new List<GeometryObject>{solid});}
  public static void Run(UIApplication ui,string folder){
   Directory.CreateDirectory(folder);string file=Path.Combine(folder,"KMep_Integration_"+DateTime.Now.ToString("HHmmss")+".rvt");File.Copy(Path.Combine(Path.GetDirectoryName(Router.Root),"LavaboConnection","Before_Lavabo_Connection_20261028_140012.rvt"),file,false);
   var d=ui.Application.OpenDocumentFile(file);var results=new List<object>();int pass=0,fail=0;
   var tests=new List<Case>{
    new Case{Name="PVC vertical down"},new Case{Name="PVC vertical up",U=XYZ.BasisZ},new Case{Name="PVC horizontal X",T=XYZ.BasisX},new Case{Name="PVC horizontal Y",T=XYZ.BasisY},
    new Case{Name="PVC main 30deg",T=new XYZ(.5,0,Math.Sqrt(.75))},new Case{Name="source skew arbitrary",U=new XYZ(.3,.4,Math.Sqrt(.75))},
    new Case{Name="source east",U=XYZ.BasisX},new Case{Name="source west",U=-XYZ.BasisX},new Case{Name="source north",U=XYZ.BasisY},new Case{Name="source south",U=-XYZ.BasisY},
    new Case{Name="PVC same tee",MainDN=21},new Case{Name="PVC 42 to 27",MainDN=42,DN=27},new Case{Name="PPR cold 32 to20",Series="PPR20",DN=20,MainDN=32},
    new Case{Name="PPR hot 32 to20",Series="PPR20",DN=20,MainDN=32,Hot=true},new Case{Name="PPR hot 50 to25",Series="PPR20",DN=25,MainDN=50,Hot=true},
    new Case{Name="existing main far connection",ConnectedMain=true},new Case{Name="wall detour",Obstacle=true},
    new Case{Name="endpoint same size",Endpoint=true,MainDN=21},new Case{Name="endpoint reducer",Endpoint=true},
    new Case{Name="endpoint arbitrary skew",Endpoint=true,T=new XYZ(.5,.5,Math.Sqrt(.5))},
    new Case{Name="lavabo actual family",Fixture=true},new Case{Name="toilet PPR actual family",Fixture=true,Toilet=true,Series="PPR20",DN=20,MainDN=32},
    new Case{Name="original lavabo and neighbouring toilet",Neighbours=true},
    new Case{Name="PPR 63 to50",Series="PPR20",DN=50,MainDN=63},new Case{Name="PVC 114 to60",DN=60,MainDN=114},
    new Case{Name="rotated linked obstacle",Linked=true},new Case{Name="modified parametric elbow H200",Flex=true},new Case{Name="straight tee",DirectTee=true,U=XYZ.BasisX},new Case{Name="short straight endpoint 100mm",ShortEnd=true,Endpoint=true,MainDN=21,T=XYZ.BasisX,U=XYZ.BasisX},new Case{Name="mandatory guide",Guided=true},new Case{Name="congested 3D shaft",Shaft=true},new Case{Name="near end reject",NearEnd=true,Expected=false,ExpectedError="quá sát"},
    new Case{Name="enclosed reject",Blocked=true,Expected=false,ExpectedError="Không"},new Case{Name="missing adapter reject",Series="PPR20",DN=21,MainDN=32,Expected=false,ExpectedError="Không có cỡ ống"},
    new Case{Name="PVC hot reject",Hot=true,Expected=false,ExpectedError="PVC-U"},new Case{Name="PPR PN10 hot reject",Hot=true,Series="PPR10",DN=20,MainDN=32,Expected=false,ExpectedError="PN20"},
    new Case{Name="wrong connected system reject",ConnectedMain=true,WrongSystem=true,Expected=false,ExpectedError="hệ khác"},new Case{Name="branch larger than main reject",DN=34,MainDN=21,Expected=false,ExpectedError="Thiếu phụ kiện"}
   };
   foreach(var row in File.ReadAllLines(Path.Combine(Router.LibraryRoot,"catalogue","fittings.csv")).Skip(1).Select(x=>x.Split(','))){if(!new[]{"Tee","ReducingTee","Socket","Reducer"}.Contains(row[1]))continue;tests.Add(new Case{Name="Catalogue "+row[0]+" "+row[1]+" "+row[2]+"x"+row[3],Series=row[0]=="PVC"?"PVC":"PPR20",DN=double.Parse(row[3],System.Globalization.CultureInfo.InvariantCulture),MainDN=double.Parse(row[2],System.Globalization.CultureInfo.InvariantCulture),Endpoint=row[1]=="Socket"||row[1]=="Reducer"});}
   try{
    string linkedFile=Path.Combine(folder,"LinkedObstacle.rvt");using(var linkdoc=ui.Application.NewProjectDocument(UnitSystem.Metric)){using(var t=new Transaction(linkdoc,"Test linked obstacle")){t.Start();Block(linkdoc,P(-100,-800,-500),P(100,800,1000));t.Commit();}linkdoc.SaveAs(linkedFile,new SaveAsOptions{OverwriteExistingFile=false});linkdoc.Close(false);}
    var level=new FilteredElementCollector(d).OfClass(typeof(Level)).Cast<Level>().First();
    foreach(var test in tests){int before=new FilteredElementCollector(d).WhereElementIsNotElementType().GetElementCount();string error=null;RouteReport report=null;bool success=false;
     using(var group=new TransactionGroup(d,"Test: "+test.Name)){group.Start();try{
      Pipe main,source;XYZ center=P(50000,50000,5000),s=center+P(-1300,-1600,500);if(test.DirectTee)s=center-P(1300,0,0);if(test.ShortEnd)s=center-test.T*Router.M(3100);Connector port;ElementId system;
      using(var t=new Transaction(d,"Create isolated test model")){t.Start();
       var type=new FilteredElementCollector(d).OfClass(typeof(PipeType)).Cast<PipeType>().First(p=>p.Name==(test.Series=="PVC"?"BMP PVC-U NhaPho mm":"BMP PPR PN"+(test.Series=="PPR10"?"10":"20")+" mm"));
       var classification=test.Hot?MEPSystemClassification.DomesticHotWater:MEPSystemClassification.DomesticColdWater;
       var sys=new FilteredElementCollector(d).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().FirstOrDefault(p=>p.SystemClassification==classification);
       if(sys==null){var basis=new FilteredElementCollector(d).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().First(p=>p.SystemClassification==MEPSystemClassification.DomesticColdWater);sys=PipingSystemType.Create(d,classification,"KMep Test Hot");}
       system=sys.Id;main=Pipe.Create(d,system,type.Id,level.Id,center-test.T*Router.M(3000),center+test.T*Router.M(3000));main.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(Router.M(test.MainDN));
       source=Pipe.Create(d,system,type.Id,level.Id,s-test.U*Router.M(600),s);source.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(Router.M(test.DN));d.Regenerate();port=Router.Ports(source).OrderBy(c=>c.Origin.DistanceTo(s)).First();
       if(test.Fixture){d.Delete(source.Id);var copyId=ElementTransformUtils.CopyElement(d,new ElementId(test.Toilet?321372L:323985L),XYZ.Zero).Single();var fixture=d.GetElement(copyId);var cold=Router.Ports(fixture).Single(c=>c.PipeSystemType==PipeSystemType.DomesticColdWater);ElementTransformUtils.MoveElement(d,copyId,s-cold.Origin);d.Regenerate();port=Router.Ports(fixture).Single(c=>c.PipeSystemType==PipeSystemType.DomesticColdWater);}
       if(test.Neighbours){d.Delete(source.Id);d.Delete(main.Id);main=(Pipe)d.GetElement(new ElementId(321434L));port=Router.Ports(d.GetElement(new ElementId(323985L))).Single(c=>c.PipeSystemType==PipeSystemType.DomesticColdWater);}
       if(test.Flex){Family family;if(!new FilteredElementCollector(d).OfClass(typeof(Family)).Cast<Family>().Any(f=>f.Name=="BMP_PVC_Elbow90"))d.LoadFamily(Path.Combine(Router.LibraryRoot,"output","FamiliesParametric","BMP_PVC_Elbow90.rfa"),out family);var symbol=new FilteredElementCollector(d).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().First(x=>x.Family.Name=="BMP_PVC_Elbow90"&&x.Name=="21 mm");symbol.LookupParameter("BMP_H_mm").Set(Router.M(200));}
       if(test.ConnectedMain){if(test.WrongSystem){var hydro=new FilteredElementCollector(d).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().First(p=>p.SystemClassification==MEPSystemClassification.SupplyHydronic);main.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).Set(hydro.Id);system=hydro.Id;}var start=center-test.T*Router.M(3000);var extra=Pipe.Create(d,system,type.Id,level.Id,start-test.T*Router.M(600),start);extra.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(Router.M(test.MainDN));d.Regenerate();Router.Ports(extra).OrderBy(c=>c.Origin.DistanceTo(start)).First().ConnectTo(Router.Ports(main).OrderBy(c=>c.Origin.DistanceTo(start)).First());}
       if(test.Obstacle)Block(d,center+P(-900,-1900,-500),center+P(-700,-300,1000));if(test.Blocked)Block(d,s-P(150,150,150),s+P(150,150,150));
       if(test.Linked){var linked=RevitLinkType.Create(d,ModelPathUtils.ConvertUserVisiblePathToModelPath(linkedFile),new RevitLinkOptions(false));var instance=RevitLinkInstance.Create(d,linked.ElementId);ElementTransformUtils.RotateElement(d,instance.Id,Line.CreateBound(XYZ.Zero,XYZ.BasisZ),Math.PI/6);ElementTransformUtils.MoveElement(d,instance.Id,center+P(-800,-1000,0));}
       if(test.Shaft){Block(d,center+P(-1000,-2500,-1000),center+P(-800,-500,800));Block(d,center+P(-2000,-1000,-1000),center+P(-500,-800,800));}
       if(t.Commit()!=TransactionStatus.Committed)throw new Exception("Test setup failed");
      }
      var selected=test.Neighbours?P(-1711.8880557,-2954.0844855,4725.0310114):test.Endpoint?center-test.T*Router.M(3000):test.NearEnd?center-test.T*Router.M(2995):center;var settings=new RouteSettings{Endpoint=test.Endpoint,Use45=true};if(test.Guided)settings.Guides.Add(center+P(-1900,-2200,-300));report=Router.Connect(d,main,port,selected,settings);if(test.Neighbours&&report.LengthMm>=2700)throw new Exception("Regression: avoid unnecessary elevation detour");success=true;
     }catch(Exception e){error=e.ToString();}finally{group.RollBack();}}
     bool restored=new FilteredElementCollector(d).WhereElementIsNotElementType().GetElementCount()==before;bool ok=success==test.Expected&&restored&&(test.Expected||(error!=null&&error.Contains(test.ExpectedError)));
     if(ok)pass++;else fail++;results.Add(new{Name=test.Name,ExpectedSuccess=test.Expected,Success=success,RollbackRestored=restored,Pass=ok,Error=error,Report=report});File.WriteAllText(Path.Combine(folder,"integration-results.json"),new JavaScriptSerializer{MaxJsonLength=10000000}.Serialize(new{Pass=pass,Fail=fail,Results=results}));
    }
   }finally{d.Close(false);}
   File.WriteAllText(Path.Combine(folder,"integration-summary.txt"),"PASS="+pass+" FAIL="+fail+" "+DateTime.Now.ToString("O"));
  }
 }
}
