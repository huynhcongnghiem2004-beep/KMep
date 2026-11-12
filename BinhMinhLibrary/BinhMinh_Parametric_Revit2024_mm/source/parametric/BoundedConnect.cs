using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace BinhMinh.ParametricConnect {
 public sealed class CataloguePart {
  public string Material,Kind,Name,TypeName; public double DN,DN2,H,H2,L,Angle;
 }
 // This engine has no application events or persistent updater. Each operation
 // receives only newly created pipes or pipes explicitly picked in its command.
 public sealed class BoundedNetwork {
  readonly Document document; readonly HashSet<long> authorized=new HashSet<long>();
  readonly List<CataloguePart> parts;
  static double M(double x){return x/304.8;}
  public BoundedNetwork(Document doc,string root,IEnumerable<ElementId> explicitlySelected) {
   document=doc;foreach(var id in explicitlySelected)authorized.Add(id.Value);
   parts=File.ReadAllLines(Path.Combine(root,"catalogue","fittings.csv")).Skip(1).Where(x=>x.Length>0).Select(x=>{
    var c=x.Split(',');Func<int,double> n=i=>double.Parse(c[i],CultureInfo.InvariantCulture);
    return new CataloguePart{Material=c[0],Kind=c[1],DN=n(2),DN2=n(3),H=n(7),H2=n(8),L=n(11),Angle=n(12),Name="BMP_"+c[0]+"_"+c[1],TypeName=c[2]+(c[2]!=c[3]?"x"+c[3]:"")+" mm"};}).ToList();
  }
  public Pipe CreatePipe(ElementId system,ElementId type,ElementId level,double diameterMm,XYZ a,XYZ b) {
   var p=Pipe.Create(document,system,type,level,a,b);p.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(M(diameterMm));authorized.Add(p.Id.Value);return p;
  }
  public Pipe SplitSelectedPipe(Pipe selected,XYZ at) {
   if(!authorized.Contains(selected.Id.Value))throw new InvalidOperationException("Lệnh chỉ được điều chỉnh ống mới hoặc ống bạn đã chọn.");
   var id=PlumbingUtils.BreakCurve(document,selected.Id,at);if(id==ElementId.InvalidElementId)throw new InvalidOperationException("Không thể tách ống đã chọn tại vị trí này.");authorized.Add(id.Value);return (Pipe)document.GetElement(id);
  }
  class End {public Pipe Pipe;public XYZ Near,Far,Out;public int Index;public double DN;}
  End GetEnd(Pipe p,XYZ junction) {
   if(!authorized.Contains(p.Id.Value))throw new InvalidOperationException("Lệnh chỉ được điều chỉnh ống mới hoặc ống bạn đã chọn.");
   var line=((LocationCurve)p.Location).Curve as Line;if(line==null)throw new InvalidOperationException("Chỉ hỗ trợ đoạn ống thẳng.");
   int i=line.GetEndPoint(0).DistanceTo(junction)<line.GetEndPoint(1).DistanceTo(junction)?0:1;
   var e=new End{Pipe=p,Near=line.GetEndPoint(i),Far=line.GetEndPoint(1-i),Index=i,DN=p.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble()*304.8};e.Out=(e.Far-e.Near).Normalize();
   var c=p.ConnectorManager.Connectors.Cast<Connector>().OrderBy(x=>x.Origin.DistanceTo(e.Near)).First();
   if(c.IsConnected)throw new InvalidOperationException("Đầu ống đã có kết nối. Hãy chọn đầu còn trống.");return e;
  }
  public FamilyInstance Connect(IList<Pipe> pipes,XYZ junction,string material) {
   if(pipes.Count!=2&&pipes.Count!=3)throw new ArgumentException("Chọn hai đầu ống hoặc ba đầu tại một vị trí tê.");
   document.Regenerate();var ends=pipes.Select(p=>GetEnd(p,junction)).ToList();
   string kind;XYZ xaxis,yaxis;double dn,dn2;List<End> ordered;
   if(ends.Count==2) {
    var a=ends[0];var b=ends[1];double angle=Math.Acos(Math.Max(-1,Math.Min(1,(-a.Out).DotProduct(b.Out))))*180/Math.PI;
    if(angle<.001) {
     if(a.DN<b.DN){var swap=a;a=b;b=swap;}kind=Math.Abs(a.DN-b.DN)<.01?"Socket":"Reducer";xaxis=-a.Out;
     yaxis=(Math.Abs(xaxis.DotProduct(XYZ.BasisZ))<.9?XYZ.BasisZ.CrossProduct(xaxis):XYZ.BasisY.CrossProduct(xaxis)).Normalize();
    } else {
     if(Math.Abs(a.DN-b.DN)>.01)throw new InvalidOperationException("Hãy tạo đoạn giảm cỡ riêng trước co.");
     if(Math.Abs(angle-45)<.001)kind="Elbow45";else if(Math.Abs(angle-90)<.001)kind="Elbow90";else throw new InvalidOperationException("Co hỗ trợ góc đúng 45° hoặc 90°. Kiểm tra lại điểm chọn và độ dốc.");
     xaxis=-a.Out;yaxis=(b.Out-xaxis*xaxis.DotProduct(b.Out)).Normalize();
    }
    dn=a.DN;dn2=b.DN;ordered=new List<End>{a,b};
   } else {
    End a=null,b=null,c=null;
    for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)if(ends[i].Out.DotProduct(ends[j].Out)<-.999999){a=ends[i];b=ends[j];c=ends[3-i-j];}
    if(a==null||Math.Abs(a.DN-b.DN)>.01)throw new InvalidOperationException("Hai đoạn ống chính của tê phải thẳng hàng và cùng cỡ.");
    xaxis=-a.Out;yaxis=c.Out;if(Math.Abs(xaxis.DotProduct(yaxis))>1e-5)throw new InvalidOperationException("Nhánh tê phải vuông góc 90° với ống chính.");
    dn=a.DN;dn2=c.DN;kind=Math.Abs(dn-dn2)<.01?"Tee":"ReducingTee";ordered=new List<End>{a,b,c};
   }
   var part=parts.FirstOrDefault(r=>r.Material==material&&r.Kind==kind&&Math.Abs(r.DN-dn)<.01&&Math.Abs(r.DN2-dn2)<.01);
   if(part==null)throw new InvalidOperationException("Tổ hợp cỡ phụ kiện này chưa có trong thư viện catalogue đã nạp.");
   var symbol=new FilteredElementCollector(document).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().FirstOrDefault(s=>s.Family.Name==part.Name&&s.Name==part.TypeName);
   if(symbol==null)throw new InvalidOperationException("Nạp thư viện BMP trước. Thiếu family: "+part.Name);
   part.H=symbol.LookupParameter("BMP_H_mm").AsDouble()*304.8;part.H2=symbol.LookupParameter("BMP_H2_mm").AsDouble()*304.8;part.L=symbol.LookupParameter("BMP_L_mm").AsDouble()*304.8;
   var local=new List<XYZ>();
   if(kind.StartsWith("Elbow")){local.Add(new XYZ(-M(part.H),0,0));double a=part.Angle*Math.PI/180;local.Add(new XYZ(M(part.H)*Math.Cos(a),M(part.H)*Math.Sin(a),0));}
   else if(kind=="Tee"||kind=="ReducingTee"){local.Add(new XYZ(-M(part.H),0,0));local.Add(new XYZ(M(part.H),0,0));local.Add(new XYZ(0,M(part.H2),0));}
   else {local.Add(new XYZ(-M(part.L)/2,0,0));local.Add(new XYZ(M(part.L)/2,0,0));}
   XYZ zaxis=xaxis.CrossProduct(yaxis).Normalize();var targets=local.Select(p=>junction+xaxis*p.X+yaxis*p.Y+zaxis*p.Z).ToList();
   // Validate every endpoint before modifying any pipe.
   for(int i=0;i<ordered.Count;i++) {
    var e=ordered[i];if((targets[i]-e.Near).CrossProduct(e.Out).GetLength()>M(.1))throw new InvalidOperationException("Trục ống không giao nhau tại vị trí nối đã chọn.");
    if((targets[i]-e.Far).GetLength()<M(10)||(targets[i]-e.Far).DotProduct(-e.Out)<M(10))throw new InvalidOperationException("Đoạn ống quá ngắn để lắp phụ kiện theo catalogue.");
   }
   if(!symbol.IsActive)symbol.Activate();document.Regenerate();
   if(symbol.Family.FamilyPlacementType!=FamilyPlacementType.WorkPlaneBased)throw new InvalidOperationException("Nạp phiên bản family BMP nhiều Type trước.");
   var planeLine=zaxis.CrossProduct(XYZ.BasisZ);if(planeLine.GetLength()<1e-8)planeLine=XYZ.BasisX;planeLine=planeLine.Normalize();
   var hostView=document.ActiveView is ViewPlan?document.ActiveView:new FilteredElementCollector(document).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().First(v=>!v.IsTemplate);
   var host=document.Create.NewReferencePlane(junction-planeLine,junction+planeLine,planeLine.CrossProduct(zaxis),hostView);host.Name="BMP fitting plane "+host.Id.Value;
   XYZ origin=junction;if(kind.StartsWith("Elbow")){double radius=part.H/Math.Tan(part.Angle*Math.PI/360);origin=junction-xaxis*M(part.H)+yaxis*M(radius);}
   var instance=document.Create.NewFamilyInstance(host.GetReference(),origin,xaxis,symbol);document.Regenerate();
   if(instance.GetTransform().BasisX.DotProduct(xaxis)<.99999||instance.GetTransform().BasisY.DotProduct(yaxis)<.99999)throw new InvalidOperationException("Không đặt được phụ kiện đúng hướng nối 3D.");
   if(host.CanBeHidden(hostView))hostView.HideElements(new List<ElementId>{host.Id});
   for(int i=0;i<ordered.Count;i++){var e=ordered[i];((LocationCurve)e.Pipe.Location).Curve=e.Index==0?Line.CreateBound(targets[i],e.Far):Line.CreateBound(e.Far,targets[i]);}
   document.Regenerate();var ports=instance.MEPModel.ConnectorManager.Connectors.Cast<Connector>().ToList();
   for(int i=0;i<ordered.Count;i++) {
    ports=instance.MEPModel.ConnectorManager.Connectors.Cast<Connector>().ToList();
    var pc=ordered[i].Pipe.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c=>c.Origin.DistanceTo(targets[i])).First();var fc=ports.OrderBy(c=>c.Origin.DistanceTo(targets[i])).First();
    if(fc.Origin.DistanceTo(pc.Origin)>M(.1)||Math.Abs(fc.Radius-pc.Radius)>M(.01))throw new InvalidOperationException("Đầu nối phụ kiện không trùng vị trí hoặc đường kính ống. Kiểm tra bộ family đã nạp.");
    pc.ConnectTo(fc);
   }
   document.Regenerate();if(instance.MEPModel.ConnectorManager.Connectors.Cast<Connector>().Any(c=>!c.IsConnected))throw new InvalidOperationException("Một đầu phụ kiện chưa kết nối được với ống.");return instance;
  }
 }
}


