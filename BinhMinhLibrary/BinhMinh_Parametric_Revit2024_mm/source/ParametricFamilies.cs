using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
namespace BinhMinhParametric {
 public static partial class Builder {
  static Autodesk.Revit.ApplicationServices.Application app;static List<string> report;
  static XYZ V(double x,double y,double z=0){return new XYZ(M(x),M(y),M(z));}
  public class Row {
   public Row Copy(){return (Row)MemberwiseClone();}
   public string Mat,Kind,Page;public double DN,DN2,PN,D,D2,H,H2,Z,Z2,L,Angle,OD,OD2;
   public string Family{get{return "BMP_"+Mat+"_"+Kind;}}public string Type{get{return DN.ToString("0.###",CultureInfo.InvariantCulture)+(DN!=DN2?"x"+DN2.ToString("0.###",CultureInfo.InvariantCulture):"")+" mm";}}
  }
  static List<Row> Rows(){var pipes=File.ReadAllLines(Path.Combine(root,"catalogue","pipes.csv")).Skip(1).Select(x=>x.Split(',')).ToList();return File.ReadAllLines(Path.Combine(root,"catalogue","fittings.csv")).Skip(1).Where(s=>s.Length>0).Select(s=>{var c=s.Split(',');Func<int,double> n=i=>double.Parse(c[i],CultureInfo.InvariantCulture);var r=new Row{Mat=c[0],Kind=c[1],DN=n(2),DN2=n(3),PN=n(4),D=n(5),D2=n(6),H=n(7),H2=n(8),Z=n(9),Z2=n(10),L=n(11),Angle=n(12),Page=c[13]};r.OD=double.Parse(pipes.First(p=>p[0]==(r.Mat=="PVC"?"PVC":"PPR20")&&double.Parse(p[1],CultureInfo.InvariantCulture)==r.DN)[2],CultureInfo.InvariantCulture);r.OD2=double.Parse(pipes.First(p=>p[0]==(r.Mat=="PVC"?"PVC":"PPR20")&&double.Parse(p[1],CultureInfo.InvariantCulture)==r.DN2)[2],CultureInfo.InvariantCulture);return r;}).ToList();}
  static FamilyParameter Formula(Document d,string name,string formula){Log("FORMULA "+name+" = "+formula);var p=d.FamilyManager.AddParameter(name,GroupTypeId.Geometry,SpecTypeId.Length,false);d.FamilyManager.SetFormula(p,formula);d.Regenerate();return p;}
  static void Text(Document d,string name,string value){var p=d.FamilyManager.AddParameter(name,GroupTypeId.IdentityData,SpecTypeId.String.Text,false);d.FamilyManager.Set(p,value);}
  static View ViewNormal(Document d,XYZ n){return new FilteredElementCollector(d).OfClass(typeof(View)).Cast<View>().First(v=>!v.IsTemplate&&(v is ViewPlan||v is ViewSection)&&Math.Abs(v.ViewDirection.Normalize().DotProduct(n.Normalize()))>.999);}
  static CurveArray Circle(XYZ center,XYZ n,double diameter){XYZ x=Math.Abs(n.Z)<.9?n.CrossProduct(XYZ.BasisZ).Normalize():XYZ.BasisX,y=n.CrossProduct(x);var a=new CurveArray();a.Append(Arc.Create(center,M(diameter/2),0,Math.PI,x,y));a.Append(Arc.Create(center,M(diameter/2),Math.PI,Math.PI*2,x,y));return a;}
  static void LabelCircles(Document d,Sketch sketch,FamilyParameter outer,FamilyParameter inner=null,bool lockCenter=true){
   var arcs=sketch.Profile.Cast<CurveArray>().SelectMany(x=>x.Cast<Curve>()).OfType<Arc>().ToList();double max=arcs.Max(a=>a.Radius);var view=ViewNormal(d,sketch.SketchPlane.GetPlane().Normal);
   foreach(var arc in arcs){var witness=arc.Center+arc.XDirection*(arc.Radius*1.5)+arc.YDirection*(arc.Radius*1.2);witness-=view.ViewDirection*((witness-view.Origin).DotProduct(view.ViewDirection));var dim=d.FamilyCreate.NewDiameterDimension(view,arc.Reference,witness);dim.FamilyLabel=inner!=null&&arc.Radius<max-1e-7?inner:outer;}
   if(!lockCenter)return;var first=arcs[0];var basisX=first.XDirection;var basisY=first.YDirection;var center=first.Center;
   foreach(var direction in new[]{basisX,basisY}){
    var datum=d.FamilyCreate.NewModelCurve(Line.CreateBound(center-direction*M(3000),center+direction*M(3000)),sketch.SketchPlane);datum.ChangeToReferenceLine();datum.Pinned=true;d.Regenerate();
    foreach(var arc in arcs.GroupBy(a=>Math.Round(a.Radius,8)).Select(g=>g.First())){var curve=(CurveElement)d.GetElement(arc.Reference.ElementId);d.FamilyCreate.NewAlignment(view,datum.GeometryCurve.Reference,curve.CenterPointReference);}
   }
  }
  static Extrusion Tube(Document d,XYZ axis,FamilyParameter diameter,FamilyParameter bore,FamilyParameter start,FamilyParameter end,bool solid=true,double planeOffset=0){
   var fm=d.FamilyManager;var loops=new CurveArrArray();loops.Append(Circle(axis*M(planeOffset),axis,fm.CurrentType.AsDouble(diameter).Value*304.8));if(bore!=null)loops.Append(Circle(axis*M(planeOffset),axis,fm.CurrentType.AsDouble(bore).Value*304.8));
   var ex=d.FamilyCreate.NewExtrusion(solid,loops,SketchPlane.Create(d,Plane.CreateByNormalAndOrigin(axis,axis*M(planeOffset))),M(10));d.Regenerate();fm.AssociateElementParameterToFamilyParameter(ex.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM),start);fm.AssociateElementParameterToFamilyParameter(ex.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM),end);LabelCircles(d,ex.Sketch,diameter,bore);return ex;
  }
  static ConnectorElement Port(Document d,Element form,XYZ direction,FamilyParameter diameter){
   d.Regenerate();PlanarFace found=null;foreach(GeometryObject g in form.get_Geometry(new Options{ComputeReferences=true,IncludeNonVisibleObjects=true})){var s=g as Solid;if(s==null)continue;foreach(Face f in s.Faces){var p=f as PlanarFace;if(p!=null){Log("FACE "+form.GetType().Name+" N="+p.FaceNormal+" O="+p.Origin);if(p.FaceNormal.DotProduct(direction)>.999&&(found==null||p.Origin.DotProduct(direction)>found.Origin.DotProduct(direction)))found=p;}}}
   if(found==null)throw new Exception("Connector face missing "+direction);var ce=ConnectorElement.CreatePipeConnector(d,PipeSystemType.Fitting,found.Reference,found.EdgeLoops.get_Item(0).get_Item(0));d.FamilyManager.AssociateElementParameterToFamilyParameter(ce.get_Parameter(BuiltInParameter.CONNECTOR_DIAMETER),diameter);return ce;
  }
  static GeomCombination Combine(Document d,params GenericForm[] forms){var a=new CombinableElementArray();foreach(var f in forms)a.Append(f);return d.CombineElements(a);}
  static void BuildAll(UIApplication ui){
   app=ui.Application;report=new List<string>{"Family,Type,Check,Result,Details"};Log("BEGIN PARAMETRIC BUILD");Directory.CreateDirectory(Path.Combine(root,"output","FamiliesParametric"));
   var groups=Rows().GroupBy(r=>r.Family).ToList();if(File.Exists(Path.Combine(root,"parametric.only"))){var only=File.ReadAllLines(Path.Combine(root,"parametric.only"));groups=groups.Where(g=>only.Contains(g.Key)).ToList();var previous=Path.Combine(root,"output","ParametricFlexTests.csv");if(File.Exists(previous))report.AddRange(File.ReadAllLines(previous).Skip(1).Where(line=>!only.Contains(line.Split(',')[0])));}if(File.Exists(Path.Combine(root,"parametric.probe")))groups=groups.Where(g=>g.Key=="BMP_PVC_Elbow90"||g.Key=="BMP_PVC_Tee"||g.Key=="BMP_PVC_Reducer").ToList();
   foreach(var group in groups){try{Build(group.ToList());}catch(Exception ex){Log("FAILED "+group.Key+" "+ex);report.Add(group.Key+",,Build,FAIL,\""+ex.Message.Replace("\"","'")+"\"");}File.WriteAllLines(Path.Combine(root,"output","ParametricFlexTests.csv"),report);}Log("PARAMETRIC BUILD FINISHED "+groups.Count+" groups");
  }
  static void SetRow(Document d,Row r){var f=d.FamilyManager;foreach(var item in new Dictionary<string,double>{{"DN",r.DN},{"DN2",r.DN2},{"D",r.D},{"D2",r.D2},{"H",r.H},{"H2",r.H2},{"Z",r.Z},{"Z2",r.Z2},{"L",r.L},{"Bore",r.OD},{"Bore2",r.OD2}})f.Set(f.get_Parameter("BMP_"+item.Key+"_mm"),M(item.Value));f.Set(f.get_Parameter("BMP_PN_bar"),r.PN);f.Set(f.get_Parameter("BMP_Catalogue_PDF_Page"),r.Page);}
  static void Build(List<Row> rows){
   Row r=rows[0];Log("BUILD "+r.Family);var d=app.NewFamilyDocument(@"C:\ProgramData\Autodesk\RVT 2024\Family Templates\English\Metric Generic Model.rft");
   try{List<ConnectorElement> ports;List<GenericForm> forms=new List<GenericForm>();
    using(var t=new Transaction(d,"Parametric catalogue dimensions and connectors")){
     t.Start();var early=t.GetFailureHandlingOptions();early.SetClearAfterRollback(true);early.SetFailuresPreprocessor(new Errors());t.SetFailureHandlingOptions(early);var fm=d.FamilyManager;d.OwnerFamily.FamilyCategory=Category.GetCategory(d,BuiltInCategory.OST_PipeFitting);d.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE).Set((int)(r.Kind.StartsWith("Elbow")?PartType.Elbow:r.Kind.Contains("Tee")?PartType.Tee:r.Kind=="Reducer"?PartType.Transition:r.Kind=="Cap"?PartType.Cap:PartType.Union));d.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED).Set(r.Kind=="Cap"?0:1);d.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL).Set(0);fm.NewType(r.Type);
     var units=d.GetUnits();units.SetFormatOptions(SpecTypeId.Length,new FormatOptions(UnitTypeId.Millimeters));d.SetUnits(units);foreach(var name in new[]{"DN","DN2","D","D2","H","H2","Z","Z2","L","Bore","Bore2"})P(d,"BMP_"+name+"_mm",10);
     var pn=fm.AddParameter("BMP_PN_bar",GroupTypeId.IdentityData,SpecTypeId.Number,false);fm.Set(pn,r.PN);Text(d,"BMP_Catalogue_PDF_Page",r.Page);Text(d,"BMP_Manufacturer","Binh Minh Plastics - independent catalogue-based BIM");Text(d,"BMP_Geometry_Note","Native parametric simplified fitting; editable type dimensions in millimeters.");Text(d,"BMP_Parametric_Revision","2.0");SetRow(d,r);
     var dn=fm.get_Parameter("BMP_DN_mm");var dn2=fm.get_Parameter("BMP_DN2_mm");var dia=fm.get_Parameter("BMP_D_mm");var dia2=fm.get_Parameter("BMP_D2_mm");var bore=fm.get_Parameter("BMP_Bore_mm");var bore2=fm.get_Parameter("BMP_Bore2_mm");ports=new List<ConnectorElement>();
     if(r.Kind.StartsWith("Elbow")){
      var angle=fm.AddParameter("Angle",GroupTypeId.Geometry,SpecTypeId.Angle,false);fm.SetFormula(angle,r.Angle.ToString(CultureInfo.InvariantCulture)+"°");var radius=Formula(d,"BMP_Bend_Radius_mm","BMP_H_mm / tan(Angle / 2)");double rad=fm.CurrentType.AsDouble(radius).Value;
      var sp=SketchPlane.Create(d,Plane.CreateByNormalAndOrigin(-XYZ.BasisX,XYZ.Zero));var profile=new CurveArrArray();profile.Append(Circle(new XYZ(0,-rad,0),XYZ.BasisX,r.D));profile.Append(Circle(new XYZ(0,-rad,0),XYZ.BasisX,r.OD));
      var revolution=d.FamilyCreate.NewRevolution(true,profile,sp,Line.CreateBound(V(0,0,1000),V(0,0,-1000)),0,r.Angle*Math.PI/180);d.Regenerate();forms.Add(revolution);fm.AssociateElementParameterToFamilyParameter(revolution.get_Parameter(BuiltInParameter.REVOLUTION_END_ANGLE),angle);LabelCircles(d,revolution.Sketch,dia,bore,false);
      var cross=d.FamilyCreate.NewModelCurve(Line.CreateBound(V(0,-3000),V(0,3000)),sp);cross.ChangeToReferenceLine();cross.Pinned=true;var axis=d.FamilyCreate.NewModelCurve(Line.CreateBound(V(0,0,-3000),V(0,0,3000)),sp);axis.ChangeToReferenceLine();axis.Pinned=true;d.Regenerate();var side=ViewNormal(d,XYZ.BasisX);
      foreach(var arc in revolution.Sketch.Profile.Cast<CurveArray>().SelectMany(a=>a.Cast<Curve>()).OfType<Arc>().GroupBy(a=>Math.Round(a.Radius,8)).Select(g=>g.First())){var center=((CurveElement)d.GetElement(arc.Reference.ElementId)).CenterPointReference;d.FamilyCreate.NewAlignment(side,cross.GeometryCurve.Reference,center);var refs=new ReferenceArray();refs.Append(axis.GeometryCurve.Reference);refs.Append(center);var dimension=d.FamilyCreate.NewLinearDimension(side,Line.CreateBound(V(0,-3000),V(0,1000)),refs);dimension.FamilyLabel=radius;}
      d.Regenerate();ports.Add(Port(d,revolution,-XYZ.BasisX,dn));ports.Add(Port(d,revolution,new XYZ(Math.Cos(r.Angle*Math.PI/180),Math.Sin(r.Angle*Math.PI/180),0),dn));foreach(var p in ports)fm.AssociateElementParameterToFamilyParameter(p.get_Parameter(BuiltInParameter.CONNECTOR_ANGLE),angle);
     }else if(r.Kind.Contains("Tee")){
      var start=Formula(d,"BMP_Main_Start_mm","-BMP_H_mm");var end=Formula(d,"BMP_Main_End_mm","BMP_H_mm");var zero=Formula(d,"BMP_Zero_mm","0 mm");var be=Formula(d,"BMP_Branch_End_mm","BMP_H2_mm");
      var va=Formula(d,"BMP_Void_Start_mm","-BMP_H_mm");var vb=Formula(d,"BMP_Void_End_mm","BMP_H_mm");var vc=Formula(d,"BMP_Void_Branch_Start_mm","0 mm");var vd=Formula(d,"BMP_Void_Branch_End_mm","BMP_H2_mm");var ringStart=Formula(d,"BMP_Branch_Ring_Start_mm","BMP_H2_mm - 3 mm");var ringEnd=Formula(d,"BMP_Branch_Ring_End_mm","BMP_H2_mm - 1 mm");var ring=Tube(d,XYZ.BasisY,dia2,null,ringStart,ringEnd,true,1);ring.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM).Set(0);var main=Tube(d,XYZ.BasisX,dia,null,start,end);var branch=Tube(d,XYZ.BasisY,dia2,null,zero,be);var vmain=Tube(d,XYZ.BasisX,bore,null,va,vb,false);var vbranch=Tube(d,XYZ.BasisY,bore2,null,vc,vd,false);var combination=Combine(d,main,branch,vmain,vbranch);d.Regenerate();forms.Add(main);forms.Add(branch);forms.Add(ring);ports.Add(Port(d,combination,-XYZ.BasisX,dn));ports.Add(Port(d,combination,XYZ.BasisX,dn));ports.Add(Port(d,ring,XYZ.BasisY,dn2));
     }else if(r.Kind=="Reducer"){
      var outer=d.FamilyCreate.NewBlend(true,Circle(V(r.L,0),XYZ.BasisX,r.D2),Circle(XYZ.Zero,XYZ.BasisX,r.D),SketchPlane.Create(d,Plane.CreateByNormalAndOrigin(XYZ.BasisX,XYZ.Zero)));d.Regenerate();var start=Formula(d,"BMP_Start_mm","-BMP_L_mm / 2");var end=Formula(d,"BMP_End_mm","BMP_L_mm / 2");fm.AssociateElementParameterToFamilyParameter(outer.get_Parameter(BuiltInParameter.BLEND_START_PARAM),start);fm.AssociateElementParameterToFamilyParameter(outer.get_Parameter(BuiltInParameter.BLEND_END_PARAM),end);LabelCircles(d,outer.BottomSketch,dia);LabelCircles(d,outer.TopSketch,dia2);
      var inner=d.FamilyCreate.NewBlend(false,Circle(V(r.L,0),XYZ.BasisX,r.OD2),Circle(XYZ.Zero,XYZ.BasisX,r.OD),SketchPlane.Create(d,Plane.CreateByNormalAndOrigin(XYZ.BasisX,XYZ.Zero)));d.Regenerate();fm.AssociateElementParameterToFamilyParameter(inner.get_Parameter(BuiltInParameter.BLEND_START_PARAM),start);fm.AssociateElementParameterToFamilyParameter(inner.get_Parameter(BuiltInParameter.BLEND_END_PARAM),end);LabelCircles(d,inner.BottomSketch,bore);LabelCircles(d,inner.TopSketch,bore2);var combination=Combine(d,outer,inner);d.Regenerate();forms.Add(outer);ports.Add(Port(d,combination,-XYZ.BasisX,dn));ports.Add(Port(d,combination,XYZ.BasisX,dn2));
     }else if(r.Kind=="Cap"){
      var start=Formula(d,"BMP_Start_mm","-BMP_L_mm");var zero=Formula(d,"BMP_Zero_mm","0 mm");var end=Formula(d,"BMP_Bore_End_mm","-BMP_Z_mm");var cutStart=Formula(d,"BMP_Bore_Start_mm","-BMP_L_mm");var body=Tube(d,XYZ.BasisX,dia,null,start,zero);var cut=Tube(d,XYZ.BasisX,bore,null,cutStart,end,false);var combination=Combine(d,body,cut);forms.Add(body);ports.Add(Port(d,combination,-XYZ.BasisX,dn));
     }else{var start=Formula(d,"BMP_Start_mm","-BMP_L_mm / 2");var end=Formula(d,"BMP_End_mm","BMP_L_mm / 2");var body=Tube(d,XYZ.BasisX,dia,bore,start,end);forms.Add(body);ports.Add(Port(d,body,-XYZ.BasisX,dn));ports.Add(Port(d,body,XYZ.BasisX,dn));}
     ports[0].AssignAsPrimary();if(ports.Count>1)ports[0].SetLinkedConnectorElement(ports[1]);var mat=(Material)d.GetElement(Material.Create(d,"BMP_"+r.Mat));mat.Color=r.Mat=="PVC"?new Color(130,135,145):new Color(25,125,220);foreach(var form in forms)form.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM).Set(mat.Id);
     foreach(var row in rows){if(row!=r)fm.NewType(row.Type);SetRow(d,row);d.Regenerate();Check(d,row,ports,"Catalogue type");}
     fm.CurrentType=fm.Types.Cast<FamilyType>().First(x=>x.Name==r.Type);SetRow(d,r);fm.Set(fm.get_Parameter("BMP_H_mm"),M(r.H*1.08));fm.Set(fm.get_Parameter("BMP_H2_mm"),M(r.H2*1.08));fm.Set(fm.get_Parameter("BMP_L_mm"),M(r.L*1.08));d.Regenerate();
     var flex=r.Copy();flex.H*=1.08;flex.H2*=1.08;flex.L*=1.08;Check(d,flex,ports,"Editable lengths +8 percent");SetRow(d,r);d.Regenerate();
     var diameterFlex=r.Copy();diameterFlex.D*=1.05;diameterFlex.D2*=1.05;diameterFlex.OD*=1.05;diameterFlex.OD2*=1.05;diameterFlex.DN*=1.05;diameterFlex.DN2*=1.05;SetRow(d,diameterFlex);d.Regenerate();Check(d,diameterFlex,ports,"Editable diameters +5 percent");SetRow(d,r);d.Regenerate();
     foreach(var curve in new FilteredElementCollector(d).OfClass(typeof(CurveElement)).OfType<ModelCurve>().Where(c=>c.IsReferenceLine)){var visible=curve.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM);if(visible!=null&&!visible.IsReadOnly)visible.Set(0);else{var visibility=new FamilyElementVisibility(FamilyElementVisibilityType.Model);visibility.IsShownInCoarse=false;visibility.IsShownInMedium=false;visibility.IsShownInFine=false;curve.SetVisibility(visibility);}}var options=t.GetFailureHandlingOptions();options.SetFailuresPreprocessor(new Errors());options.SetClearAfterRollback(true);t.SetFailureHandlingOptions(options);if(t.Commit()!=TransactionStatus.Committed)throw new Exception("Family commit rolled back");
    }
    d.SaveAs(Path.Combine(root,"output","FamiliesParametric",r.Family+".rfa"),new SaveAsOptions{OverwriteExistingFile=true,MaximumBackups=1});Log("SAVED "+r.Family+" types="+rows.Count);
   }finally{d.Close(false);}
  }
  static void Check(Document d,Row r,List<ConnectorElement> ports,string check){
   foreach(var dim in new FilteredElementCollector(d).OfClass(typeof(Dimension)).Cast<Dimension>()){FamilyParameter label;try{label=dim.FamilyLabel;}catch(Autodesk.Revit.Exceptions.InvalidOperationException){continue;}if(label!=null&&dim.Value.HasValue){var expected=d.FamilyManager.CurrentType.AsDouble(label);if(expected.HasValue&&Math.Abs(dim.Value.Value-expected.Value)>M(.01))throw new Exception("Labelled native geometry dimension did not flex: "+label.Definition.Name);}}
   var points=new List<XYZ>();var diameters=new List<double>();if(r.Kind.StartsWith("Elbow")){double a=r.Angle*Math.PI/180,rad=r.H/Math.Tan(a/2);points.Add(V(0,-rad));points.Add(V(rad*Math.Sin(a),-rad*Math.Cos(a)));diameters.AddRange(new[]{r.DN,r.DN});}else if(r.Kind.Contains("Tee")){points.Add(V(-r.H,0));points.Add(V(r.H,0));points.Add(V(0,r.H2));diameters.AddRange(new[]{r.DN,r.DN,r.DN2});}else if(r.Kind=="Cap"){points.Add(V(-r.L,0));diameters.Add(r.DN);}else{points.Add(V(-r.L/2,0));points.Add(V(r.L/2,0));diameters.AddRange(new[]{r.DN,r.DN2});}
   for(int i=0;i<ports.Count;i++){if(ports[i].Origin.DistanceTo(points[i])>M(.02)){foreach(var ce in new FilteredElementCollector(d).OfClass(typeof(CurveElement)).Cast<CurveElement>()){var ac=ce.GeometryCurve as Arc;if(ac!=null)Log("DEBUG ARC "+ce.Id+" C="+ac.Center+" R="+ac.Radius*304.8);}throw new Exception("Connector position mismatch "+r.Type+" port="+i+" actual="+ports[i].Origin+" expected="+points[i]);}if(Math.Abs(ports[i].get_Parameter(BuiltInParameter.CONNECTOR_DIAMETER).AsDouble()*304.8-diameters[i])>.01)throw new Exception("Connector diameter mismatch "+r.Type);}report.Add(r.Family+","+r.Type+","+check+",PASS,Native geometry and connectors flexed");Log("PASS "+r.Family+" "+r.Type+" "+check);
  }
  class Errors:IFailuresPreprocessor{public FailureProcessingResult PreprocessFailures(FailuresAccessor a){bool fail=false;foreach(var m in a.GetFailureMessages()){if(m.GetSeverity()==FailureSeverity.Warning)a.DeleteWarning(m);else{Log("ERROR "+m.GetDescriptionText());fail=true;}}return fail?FailureProcessingResult.ProceedWithRollBack:FailureProcessingResult.Continue;}}
 }
}


















