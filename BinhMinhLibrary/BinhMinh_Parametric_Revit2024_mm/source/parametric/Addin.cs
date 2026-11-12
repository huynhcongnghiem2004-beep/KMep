using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using F=System.Windows.Forms;
using Drawing=System.Drawing;

namespace BinhMinh.ParametricConnect {
 public sealed class App:IExternalApplication {
  public Result OnStartup(UIControlledApplication a) {
   try{a.CreateRibbonTab("KMep");}catch(Autodesk.Revit.Exceptions.ArgumentException){}
   var p=a.GetRibbonPanels("KMep").FirstOrDefault(x=>x.Name=="Bình Minh Parametric")??a.CreateRibbonPanel("KMep","Bình Minh Parametric");AddButtons(p);return Result.Succeeded;
  }
  public static void AddToSession(UIApplication a) {
   try{a.CreateRibbonTab("KMep");}catch(Autodesk.Revit.Exceptions.ArgumentException){}
   var p=a.GetRibbonPanels("KMep").FirstOrDefault(x=>x.Name=="Bình Minh Parametric")??a.CreateRibbonPanel("KMep","Bình Minh Parametric");AddButtons(p);
  }
  static void AddButtons(RibbonPanel p) {
   Add(p,"BMP2024.Setup","Nạp\nthư viện",typeof(SetupCommand),"Nạp family, cỡ ống mm và Pipe Types Bình Minh vào dự án hiện tại.");
   Add(p,"BMP2024.Draw","Vẽ ống\nBình Minh",typeof(DrawCommand),"Vẽ các đoạn ống mới và tự chèn đúng co 45°/90°, nối hoặc giảm cỡ. Esc để kết thúc. Chỉ điều chỉnh ống mới trong lệnh và đầu ống bạn chọn làm điểm bắt đầu.");
   Add(p,"BMP2024.Branch","Vẽ nhánh\nBình Minh",typeof(BranchCommand),"Chọn một ống chính Bình Minh, chọn vị trí nhánh và đầu nhánh. Lệnh tách đúng ống đã chọn, chèn tê hoặc tê giảm theo catalogue.");
   Add(p,"BMP2024.Join","Nối hai\nđầu ống",typeof(JoinCommand),"Chọn hai ống Bình Minh có đầu còn trống. Lệnh chỉ điều chỉnh hai đầu được chọn để nối bằng phụ kiện đúng cỡ/góc.");
   Add(p,"BMP2024.Cap","Bịt đầu\nống",typeof(CapCommand),"Chọn một ống Bình Minh để bịt các đầu còn trống bằng nắp đúng cỡ.");
  }
  static void Add(RibbonPanel p,string id,string label,Type type,string tip) {
   id=id.Replace("BMP2024.","BMPParam2024.");if(p.GetItems().Any(x=>x.Name==id))return;var b=new PushButtonData(id,label,Assembly.GetExecutingAssembly().Location,type.FullName);b.ToolTip=tip;p.AddItem(b);
  }
  public Result OnShutdown(UIControlledApplication a){return Result.Succeeded;}
 }
 public static class Library {
  public static string Root{get{return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);}}
  public static double Mm(double x){return x/304.8;}
  public static string Series(PipeType type){return type.Name.Contains("PVC")?"PVC":type.Name.Contains("PN10")?"PPR10":"PPR20";}
  public static string Material(PipeType type){return Series(type)=="PVC"?"PVC":"PPR";}
  public static bool IsBmp(Pipe pipe){return pipe!=null&&((PipeType)pipe.Document.GetElement(pipe.GetTypeId())).Name.StartsWith("BMP ");}
  public static void EnsureProject(Document d){if(d.IsFamilyDocument)throw new InvalidOperationException("Mở một dự án Revit để vẽ ống.");}
  public static ElementId System(Document d,MEPSystemClassification classification){return new FilteredElementCollector(d).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().First(x=>x.SystemClassification==classification).Id;}
  public static void Load(Document d) {
   EnsureProject(d);
   using(var t=new Transaction(d,"Nạp thư viện ống Bình Minh mm")) {
    t.Start();
    var loaded=new HashSet<string>(new FilteredElementCollector(d).OfClass(typeof(Family)).Cast<Family>().Select(f=>f.Name));
   foreach(string name in File.ReadAllLines(Path.Combine(Root,"catalogue","fittings.csv")).Skip(1).Where(s=>s.Length>0).Select(s=>s.Split(',')).Select(c=>"BMP_"+c[0]+"_"+c[1]).Distinct()) {
    if(!loaded.Contains(name)){Family f;if(!d.LoadFamily(Path.Combine(Root,"output","FamiliesParametric",name+".rfa"),out f))throw new InvalidOperationException("Không nạp được family "+name);loaded.Add(name);}
    }
    var data=File.ReadAllLines(Path.Combine(Root,"catalogue","pipes.csv")).Skip(1).Where(s=>s.Length>0).Select(s=>s.Split(',')).ToList();
    var baseType=new FilteredElementCollector(d).OfClass(typeof(PipeType)).Cast<PipeType>().FirstOrDefault();if(baseType==null)throw new InvalidOperationException("Dự án cần một Pipe Type cơ sở. Hãy mở dự án mẫu MEP hoặc file thư viện BMP đi kèm.");
    foreach(string series in new[]{"PVC","PPR10","PPR20"}) {
     string name=series=="PVC"?"BMP PVC-U NhaPho mm":"BMP PPR PN"+(series=="PPR10"?"10":"20")+" mm";
     var existingType=new FilteredElementCollector(d).OfClass(typeof(PipeType)).Cast<PipeType>().FirstOrDefault(x=>x.Name==name);
     PipeSegment segment=null;
     if(existingType!=null){var rules=existingType.RoutingPreferenceManager;if(rules.GetNumberOfRules(RoutingPreferenceRuleGroupType.Segments)>0)segment=d.GetElement(rules.GetRule(RoutingPreferenceRuleGroupType.Segments,0).MEPPartId) as PipeSegment;}
     if(segment==null){
     var material=(Material)d.GetElement(Autodesk.Revit.DB.Material.Create(d,"BMP material "+series+" "+Guid.NewGuid().ToString("N").Substring(0,6)));material.Color=series=="PVC"?new Color(130,135,145):new Color(25,125,220);
     var schedule=PipeScheduleType.Create(d,name+" "+Guid.NewGuid().ToString("N").Substring(0,6));
     Func<string,double> n=s=>double.Parse(s,global::System.Globalization.CultureInfo.InvariantCulture);
     var sizes=data.Where(c=>c[0]==series).Select(c=>new MEPSize(Mm(n(c[1])),Mm(n(c[2])-2*n(c[3])),Mm(n(c[2])),true,true)).ToList();
     segment=PipeSegment.Create(d,material.Id,schedule.Id,sizes);segment.Roughness=Mm(.007);
     }
     var type=existingType??(PipeType)baseType.Duplicate(name);var rpm=type.RoutingPreferenceManager;
     foreach(RoutingPreferenceRuleGroupType g in Enum.GetValues(typeof(RoutingPreferenceRuleGroupType)))if(g!=RoutingPreferenceRuleGroupType.Undefined)for(int i=rpm.GetNumberOfRules(g)-1;i>=0;i--)rpm.RemoveRule(g,i);
     var sr=new RoutingPreferenceRule(segment.Id,name);sr.AddCriterion(PrimarySizeCriterion.All());rpm.AddRule(RoutingPreferenceRuleGroupType.Segments,sr);rpm.PreferredJunctionType=PreferredJunctionType.Tee;
     string prefix=series=="PVC"?"BMP_PVC_":"BMP_PPR_";
     foreach(var symbol in new FilteredElementCollector(d).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().Where(s=>s.Family.Name.StartsWith(prefix)&&s.LookupParameter("BMP_Parametric_Revision")!=null).OrderBy(s=>s.Family.Name).ThenBy(s=>s.LookupParameter("BMP_DN_mm").AsDouble()).ThenBy(s=>s.LookupParameter("BMP_DN2_mm").AsDouble())) {
      string fn=symbol.Family.Name;var group=fn.Contains("_Elbow")?RoutingPreferenceRuleGroupType.Elbows:fn.EndsWith("Tee")?RoutingPreferenceRuleGroupType.Junctions:fn.EndsWith("_Reducer")?RoutingPreferenceRuleGroupType.Transitions:fn.EndsWith("_Cap")?RoutingPreferenceRuleGroupType.Caps:RoutingPreferenceRuleGroupType.Unions;
      double diameter=symbol.LookupParameter("BMP_DN_mm").AsDouble();var rule=new RoutingPreferenceRule(symbol.Id,fn);rule.AddCriterion(new PrimarySizeCriterion(diameter-Mm(.01),diameter+Mm(.01)));rpm.AddRule(group,rule);
     }
    }
    foreach(var system in new[]{MEPSystemClassification.DomesticColdWater,MEPSystemClassification.DomesticHotWater,MEPSystemClassification.Sanitary,MEPSystemClassification.Vent})if(!new FilteredElementCollector(d).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().Any(s=>s.SystemClassification==system))PipingSystemType.Create(d,system,"BMP "+system);
    if(t.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException("Không lưu được thư viện vào dự án.");
   }
  }
  public static XYZ Intersection(Pipe a,Pipe b) {
   var la=(Line)((LocationCurve)a.Location).Curve;var lb=(Line)((LocationCurve)b.Location).Curve;XYZ u=la.Direction,v=lb.Direction,w=la.GetEndPoint(0)-lb.GetEndPoint(0);double uv=u.DotProduct(v),den=1-uv*uv;
   if(den<1e-9) {
    if(w.CrossProduct(u).GetLength()>Mm(.1))throw new InvalidOperationException("Hai ống không thẳng hàng.");
    var pairs=from p in new[]{la.GetEndPoint(0),la.GetEndPoint(1)} from q in new[]{lb.GetEndPoint(0),lb.GetEndPoint(1)} select new{P=p,Q=q,D=p.DistanceTo(q)};var near=pairs.OrderBy(x=>x.D).First();return (near.P+near.Q)*.5;
   }
   double t=(uv*v.DotProduct(w)-u.DotProduct(w))/den;XYZ point=la.GetEndPoint(0)+u*t;if((point-lb.GetEndPoint(0)).CrossProduct(v).GetLength()>Mm(.1))throw new InvalidOperationException("Hai trục ống không giao nhau.");return point;
  }
 }
 sealed class PipeFilter:ISelectionFilter{public bool AllowElement(Element e){return Library.IsBmp(e as Pipe);}public bool AllowReference(Reference r,XYZ p){return true;}}
 sealed class Options:F.Form {
  readonly F.ComboBox type=new F.ComboBox(),size=new F.ComboBox(),system=new F.ComboBox();readonly F.NumericUpDown offset=new F.NumericUpDown();readonly List<PipeType> types;
  public PipeType PipeType{get{return types[type.SelectedIndex];}} public double Diameter{get{return double.Parse(size.SelectedItem.ToString(),global::System.Globalization.CultureInfo.InvariantCulture);}}
  public double Offset{get{return (double)offset.Value;}} public MEPSystemClassification Classification{get{return new[]{MEPSystemClassification.DomesticColdWater,MEPSystemClassification.DomesticHotWater,MEPSystemClassification.Sanitary,MEPSystemClassification.Vent}[system.SelectedIndex];}}
  public Options(Document d,Pipe selected=null) {
   Text="Bình Minh — Vẽ và tự nối";ClientSize=new Drawing.Size(485,290);FormBorderStyle=F.FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=F.FormStartPosition.CenterScreen;Font=new Drawing.Font("Segoe UI",10);
   types=new FilteredElementCollector(d).OfClass(typeof(PipeType)).Cast<PipeType>().Where(p=>p.Name.StartsWith("BMP ")).OrderBy(p=>p.Name).ToList();if(types.Count==0)throw new InvalidOperationException("Bấm Nạp thư viện trước khi vẽ.");
   var table=new F.TableLayoutPanel{Dock=F.DockStyle.Fill,Padding=new F.Padding(16),ColumnCount=2,RowCount=6};table.ColumnStyles.Add(new F.ColumnStyle(F.SizeType.Absolute,145));table.ColumnStyles.Add(new F.ColumnStyle(F.SizeType.Percent,100));Controls.Add(table);
   Action<int,string,F.Control> row=(i,label,control)=>{table.RowStyles.Add(new F.RowStyle(F.SizeType.Absolute,37));table.Controls.Add(new F.Label{Text=label,AutoSize=true,Anchor=F.AnchorStyles.Left},0,i);control.Dock=F.DockStyle.Fill;table.Controls.Add(control,1,i);};
   type.DropDownStyle=size.DropDownStyle=system.DropDownStyle=F.ComboBoxStyle.DropDownList;type.Items.AddRange(types.Select(p=>p.Name).ToArray());
   type.SelectedIndexChanged+=(s,e)=>{size.Items.Clear();string series=Library.Series(PipeType);foreach(var c in File.ReadAllLines(Path.Combine(Library.Root,"catalogue","pipes.csv")).Skip(1).Where(x=>x.StartsWith(series+",")).Select(x=>x.Split(',')))size.Items.Add(c[1]);if(size.Items.Count>0)size.SelectedIndex=0;};
   row(0,"Loại ống",type);row(1,"Đường kính (mm)",size);system.Items.AddRange(new object[]{"Cấp nước lạnh","Cấp nước nóng","Thoát nước thải","Thông hơi"});system.SelectedIndex=0;row(2,"Hệ thống",system);
   offset.Minimum=-10000;offset.Maximum=100000;offset.Increment=50;offset.Value=1000;row(3,"Cao độ so Level (mm)",offset);
   var note=new F.Label{Text="Co 45°/90° và phụ kiện đúng cỡ được chèn khi dùng lệnh BMP. Esc kết thúc chuỗi ống.",Dock=F.DockStyle.Fill,AutoSize=false};table.Controls.Add(note,0,4);table.SetColumnSpan(note,2);table.RowStyles.Add(new F.RowStyle(F.SizeType.Absolute,52));
   var buttons=new F.FlowLayoutPanel{Dock=F.DockStyle.Fill,FlowDirection=F.FlowDirection.RightToLeft};var ok=new F.Button{Text="Bắt đầu",DialogResult=F.DialogResult.OK,AutoSize=true};var cancel=new F.Button{Text="Hủy",DialogResult=F.DialogResult.Cancel,AutoSize=true};buttons.Controls.Add(ok);buttons.Controls.Add(cancel);table.Controls.Add(buttons,0,5);table.SetColumnSpan(buttons,2);AcceptButton=ok;CancelButton=cancel;
   int index=selected==null?-1:types.FindIndex(p=>p.Id==selected.GetTypeId());type.SelectedIndex=index>=0?index:types.FindIndex(p=>p.Name.Contains("PN20"));if(type.SelectedIndex<0)type.SelectedIndex=0;
   if(selected!=null){string dn=(selected.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble()*304.8).ToString("0.###",global::System.Globalization.CultureInfo.InvariantCulture);if(size.Items.Contains(dn))size.SelectedItem=dn;
    var st=d.GetElement(selected.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId()) as PipingSystemType;var classes=new[]{MEPSystemClassification.DomesticColdWater,MEPSystemClassification.DomesticHotWater,MEPSystemClassification.Sanitary,MEPSystemClassification.Vent};if(st!=null){int si=Array.IndexOf(classes,st.SystemClassification);if(si>=0)system.SelectedIndex=si;}system.Enabled=false;offset.Enabled=false;
    var curve=((LocationCurve)selected.Location).Curve;decimal mm=(decimal)((curve.GetEndPoint(0).Z-selected.ReferenceLevel.Elevation)*304.8);offset.Value=Math.Max(offset.Minimum,Math.Min(offset.Maximum,mm));
   }
  }
 }
 sealed class RollbackErrors:IFailuresPreprocessor {
  public string Error;
  public FailureProcessingResult PreprocessFailures(FailuresAccessor a){bool fail=false;foreach(var m in a.GetFailureMessages()){if(m.GetSeverity()==FailureSeverity.Warning)a.DeleteWarning(m);else{fail=true;Error=m.GetDescriptionText();}}return fail?FailureProcessingResult.ProceedWithRollBack:FailureProcessingResult.Continue;}
  public static void Set(Transaction t,RollbackErrors handler){var f=t.GetFailureHandlingOptions();f.SetFailuresPreprocessor(handler);f.SetClearAfterRollback(true);t.SetFailureHandlingOptions(f);}
 }
 [Transaction(TransactionMode.Manual)]public sealed class SetupCommand:IExternalCommand {
  public Result Execute(ExternalCommandData c,ref string message,ElementSet e){try{Library.Load(c.Application.ActiveUIDocument.Document);TaskDialog.Show("Bình Minh 2024","Đã nạp thư viện mm. Dùng Vẽ ống Bình Minh để tự chèn đúng co và phụ kiện giảm cỡ.");return Result.Succeeded;}catch(Exception ex){message=ex.Message;return Result.Failed;}}
 }
 [Transaction(TransactionMode.Manual)]public sealed class DrawCommand:IExternalCommand {
  public Result Execute(ExternalCommandData c,ref string message,ElementSet e) {
   var ui=c.Application.ActiveUIDocument;var d=ui.Document;
   try {
    Library.EnsureProject(d);var level=d.ActiveView.GenLevel;if(level==null)throw new InvalidOperationException("Mở mặt bằng Level để vẽ chuỗi ống.");
    var ids=ui.Selection.GetElementIds();Pipe selected=ids.Count==1?d.GetElement(ids.First()) as Pipe:null;if(!Library.IsBmp(selected))selected=null;
    using(var o=new Options(d,selected)) {
     if(o.ShowDialog()!=F.DialogResult.OK)return Result.Cancelled;
     if(selected!=null&&Library.Material((PipeType)d.GetElement(selected.GetTypeId()))!=Library.Material(o.PipeType))throw new InvalidOperationException("Không nối trực tiếp PVC sang PPR. Hãy dùng bộ chuyển phù hợp.");
     var allowed=selected==null?new ElementId[0]:new[]{selected.Id};var network=new BoundedNetwork(d,Library.Root,allowed);Pipe previous=selected;XYZ start;
     double z=level.Elevation+Library.Mm(o.Offset);
     if(selected!=null){var free=selected.ConnectorManager.Connectors.Cast<Connector>().Where(x=>!x.IsConnected).ToList();if(free.Count==0)throw new InvalidOperationException("Ống đã chọn không còn đầu trống.");XYZ pick=free.Count==1?free[0].Origin:ui.Selection.PickPoint("Chọn đầu trống của ống đã chọn");start=free.OrderBy(x=>x.Origin.DistanceTo(pick)).First().Origin;z=start.Z;}
     else{start=ui.Selection.PickPoint("Chọn điểm đầu chuỗi ống Bình Minh");start=new XYZ(start.X,start.Y,z);}
     using(var group=new TransactionGroup(d,"Vẽ và tự nối ống Bình Minh")) {
      group.Start();while(true) {
       XYZ end;try{var picked=ui.Selection.PickPoint("Chọn điểm tiếp theo — co 45°/90°; Esc kết thúc");end=new XYZ(picked.X,picked.Y,z);}catch(Autodesk.Revit.Exceptions.OperationCanceledException){break;}
       if(end.DistanceTo(start)<Library.Mm(10))continue;
       using(var t=new Transaction(d,"Tạo đoạn ống và phụ kiện Bình Minh")) {
        t.Start();var failures=new RollbackErrors();RollbackErrors.Set(t,failures);
        try{var systemId=selected!=null?selected.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId():Library.System(d,o.Classification);var next=network.CreatePipe(systemId,o.PipeType.Id,level.Id,o.Diameter,start,end);if(previous!=null)network.Connect(new List<Pipe>{previous,next},start,Library.Material(o.PipeType));if(t.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException(failures.Error??"Không tạo được kết nối.");previous=next;start=end;}
        catch(Exception ex){if(t.GetStatus()==TransactionStatus.Started)t.RollBack();TaskDialog.Show("Bình Minh 2024",ex.Message+"\nĐoạn vừa thử đã được hủy. Các đoạn đã tạo trước đó được giữ lại.");break;}
       }
      }group.Assimilate();
     }
    }return Result.Succeeded;
   }catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}catch(Exception ex){message=ex.Message;return Result.Failed;}
  }
 }
 [Transaction(TransactionMode.Manual)]public sealed class JoinCommand:IExternalCommand {
  public Result Execute(ExternalCommandData c,ref string message,ElementSet e){var ui=c.Application.ActiveUIDocument;var d=ui.Document;try{
   Library.EnsureProject(d);var a=(Pipe)d.GetElement(ui.Selection.PickObject(ObjectType.Element,new PipeFilter(),"Chọn ống BMP thứ nhất — chỉ đầu trống được điều chỉnh"));var b=(Pipe)d.GetElement(ui.Selection.PickObject(ObjectType.Element,new PipeFilter(),"Chọn ống BMP thứ hai"));if(a.Id==b.Id)throw new InvalidOperationException("Chọn hai ống khác nhau.");
   var type=(PipeType)d.GetElement(a.GetTypeId());if(Library.Material(type)!=Library.Material((PipeType)d.GetElement(b.GetTypeId())))throw new InvalidOperationException("Hai ống phải cùng vật liệu PVC hoặc PPR.");
   using(var t=new Transaction(d,"Nối hai đầu ống BMP đã chọn")){t.Start();var fail=new RollbackErrors();RollbackErrors.Set(t,fail);new BoundedNetwork(d,Library.Root,new[]{a.Id,b.Id}).Connect(new List<Pipe>{a,b},Library.Intersection(a,b),Library.Material(type));if(t.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException(fail.Error??"Không tạo được kết nối.");}return Result.Succeeded;
  }catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}catch(Exception ex){message=ex.Message;return Result.Failed;}}
 }
 [Transaction(TransactionMode.Manual)]public sealed class BranchCommand:IExternalCommand {
  public Result Execute(ExternalCommandData c,ref string message,ElementSet e){var ui=c.Application.ActiveUIDocument;var d=ui.Document;try{
   Library.EnsureProject(d);var r=ui.Selection.PickObject(ObjectType.Element,new PipeFilter(),"Chọn đúng vị trí trên ống chính BMP để tách và chèn tê");var main=(Pipe)d.GetElement(r);var line=((LocationCurve)main.Location).Curve as Line;if(line==null)throw new InvalidOperationException("Chọn ống chính thẳng.");var at=line.Project(r.GlobalPoint).XYZPoint;
   using(var o=new Options(d,main)){if(o.ShowDialog()!=F.DialogResult.OK)return Result.Cancelled;var mainType=(PipeType)d.GetElement(main.GetTypeId());if(Library.Material(mainType)!=Library.Material(o.PipeType))throw new InvalidOperationException("Nhánh phải cùng vật liệu với ống chính.");var end=ui.Selection.PickPoint("Chọn đầu nhánh vuông góc với ống chính");end=new XYZ(end.X,end.Y,at.Z);if(end.DistanceTo(at)<Library.Mm(20))throw new InvalidOperationException("Nhánh quá ngắn.");if(Math.Abs((end-at).Normalize().DotProduct(line.Direction))>1e-5)throw new InvalidOperationException("Vẽ nhánh vuông góc 90° với ống chính.");
    using(var t=new Transaction(d,"Tách đúng ống đã chọn và chèn tê Bình Minh")){t.Start();var failures=new RollbackErrors();RollbackErrors.Set(t,failures);var network=new BoundedNetwork(d,Library.Root,new[]{main.Id});var second=network.SplitSelectedPipe(main,at);var branch=network.CreatePipe(main.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId(),o.PipeType.Id,main.ReferenceLevel.Id,o.Diameter,at,end);network.Connect(new List<Pipe>{main,second,branch},at,Library.Material(mainType));if(t.Commit()!=TransactionStatus.Committed)throw new InvalidOperationException(failures.Error??"Không chèn được tê.");}
   }return Result.Succeeded;
  }catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}catch(Exception ex){message=ex.Message;return Result.Failed;}}
 }
 [Transaction(TransactionMode.Manual)]public sealed class CapCommand:IExternalCommand {
  public Result Execute(ExternalCommandData c,ref string message,ElementSet e){var ui=c.Application.ActiveUIDocument;var d=ui.Document;try{Library.EnsureProject(d);var pipe=(Pipe)d.GetElement(ui.Selection.PickObject(ObjectType.Element,new PipeFilter(),"Chọn ống BMP để bịt các đầu còn trống"));using(var t=new Transaction(d,"Bịt đầu ống BMP đã chọn")){t.Start();PlumbingUtils.PlaceCapOnOpenEnds(d,pipe.Id,ElementId.InvalidElementId);t.Commit();}return Result.Succeeded;}catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}catch(Exception ex){message=ex.Message;return Result.Failed;}}
 }
}



