using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using BinhMinh.ParametricConnect;
namespace KMep.Supply {
 public sealed class RouteSettings {public double ClearanceMm=10,SearchMm=2000,LaunchMm=0;public bool Use45=true,Endpoint;public IList<XYZ> Guides=new List<XYZ>();}
 public sealed class RouteReport {public string Status,Document,Material,System,Reason;public long Source,Target;public double BranchMm,MainMm,LengthMm;public int SearchStates,UnloadedLinks;public long[] Created;public string[] Fittings;public double[][] RouteMm;}
 public static class Router {
  public static readonly string Root=Path.GetDirectoryName(typeof(Router).Assembly.Location);
  public static readonly string LibraryRoot=Path.Combine(Path.GetDirectoryName(Root),"BinhMinhLibrary","BinhMinh_Parametric_Revit2024_mm");
  public static double M(double n){return n/304.8;}static double Mm(double n){return n*304.8;}
  public static IEnumerable<Connector> Ports(Element e){var p=e as MEPCurve;if(p!=null)return p.ConnectorManager.Connectors.Cast<Connector>().Where(c=>c.Domain==Domain.DomainPiping&&c.ConnectorType==ConnectorType.End);var f=e as FamilyInstance;return f!=null&&f.MEPModel!=null&&f.MEPModel.ConnectorManager!=null?f.MEPModel.ConnectorManager.Connectors.Cast<Connector>().Where(c=>c.Domain==Domain.DomainPiping&&c.ConnectorType==ConnectorType.End):Enumerable.Empty<Connector>();}
  static bool Supply(PipeSystemType t){return t==PipeSystemType.DomesticColdWater||t==PipeSystemType.DomesticHotWater;}
  public static bool IsSupply(Connector c){return c.Domain==Domain.DomainPiping&&Supply(c.PipeSystemType);}
  public static bool IsBmp(Pipe p){return Library.IsBmp(p);}
  sealed class Frame {public XYZ O,X,Y,Z;public V Local(XYZ p){var d=p-O;return new V(Mm(d.DotProduct(X)),Mm(d.DotProduct(Y)),Mm(d.DotProduct(Z)));}public XYZ World(V p){return O+X*M(p.X)+Y*M(p.Y)+Z*M(p.Z);}}
  sealed class Obstacle {public long Id;public List<Solid> Solids=new List<Solid>();public BoundingBoxXYZ Bounds;public Transform Transform=Transform.Identity;}
  static IEnumerable<Solid> Solids(GeometryElement g,Transform t){if(g==null)yield break;foreach(var o in g){var solid=o as Solid;if(solid!=null&&solid.Volume>1e-10){yield return SolidUtils.CreateTransformed(solid,t);continue;}var instance=o as GeometryInstance;if(instance!=null)foreach(var s in Solids(instance.GetSymbolGeometry(),t.Multiply(instance.Transform)))yield return s;}}
  static List<XYZ> Corners(BoundingBoxXYZ b,Transform t){var r=new List<XYZ>();for(int i=0;i<8;i++)r.Add(t.OfPoint(b.Transform.OfPoint(new XYZ((i&1)==0?b.Min.X:b.Max.X,(i&2)==0?b.Min.Y:b.Max.Y,(i&4)==0?b.Min.Z:b.Max.Z))));return r;}
  static bool Relevant(Element e){return e.Category!=null&&e.Category.CategoryType==CategoryType.Model&&!(e is ReferencePlane)&&!(e is RevitLinkInstance)&&!(e is SpatialElement);}
  static List<Obstacle> Collect(Document d,HashSet<long> excluded,out int unloaded){
   unloaded=0;var result=new List<Obstacle>();var opts=new Options{DetailLevel=ViewDetailLevel.Fine,IncludeNonVisibleObjects=false};
   Action<Document,Transform,bool> collect=(doc,t,isLink)=>{foreach(var e in new FilteredElementCollector(doc).WhereElementIsNotElementType().Where(Relevant)){
    if(!isLink&&excluded.Contains(e.Id.Value))continue;var bounds=e.get_BoundingBox(null);if(bounds==null)continue;var solids=Solids(e.get_Geometry(opts),t).ToList();if(solids.Count==0)continue;
    result.Add(new Obstacle{Id=isLink?-e.Id.Value:e.Id.Value,Bounds=bounds,Transform=t,Solids=solids});
   }};
   collect(d,Transform.Identity,false);int missing=0;Action<Document,Transform,HashSet<string>> links=null;
   links=(doc,parent,ancestors)=>{foreach(var link in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()){var ld=link.GetLinkDocument();if(ld==null){missing++;continue;}string key=ld.PathName;if(ancestors.Contains(key))continue;var transform=parent.Multiply(link.GetTotalTransform());collect(ld,transform,true);var chain=new HashSet<string>(ancestors);chain.Add(key);links(ld,transform,chain);}};
   links(d,Transform.Identity,new HashSet<string>{d.PathName});unloaded=missing;return result;
  }
  static Box LocalBox(Obstacle o,Frame frame){var p=Corners(o.Bounds,o.Transform).Select(frame.Local).ToList();return new Box(new V(p.Min(v=>v.X),p.Min(v=>v.Y),p.Min(v=>v.Z)),new V(p.Max(v=>v.X),p.Max(v=>v.Y),p.Max(v=>v.Z)),o.Id);}
  static List<string[]> Catalogue(){return File.ReadAllLines(Path.Combine(LibraryRoot,"catalogue","fittings.csv")).Skip(1).Where(x=>x.Length>0).Select(x=>x.Split(',')).ToList();}
  static void ActualDimensions(Document d,List<string[]> rows){var symbols=new FilteredElementCollector(d).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();foreach(var row in rows){string type=row[2]+(row[2]==row[3]?"":"x"+row[3])+" mm";var symbol=symbols.FirstOrDefault(s=>s.Family.Name=="BMP_"+row[0]+"_"+row[1]&&s.Name==type);if(symbol==null)continue;foreach(var item in new[]{new{I=5,P="BMP_D_mm"},new{I=6,P="BMP_D2_mm"},new{I=7,P="BMP_H_mm"},new{I=8,P="BMP_H2_mm"},new{I=11,P="BMP_L_mm"}}){var p=symbol.LookupParameter(item.P);if(p!=null)row[item.I]=Mm(p.AsDouble()).ToString(CultureInfo.InvariantCulture);}}}
  static double N(string s){return double.Parse(s,CultureInfo.InvariantCulture);}
  static string[] Part(List<string[]> rows,string mat,string kind,double dn,double dn2){return rows.FirstOrDefault(c=>c[0]==mat&&c[1]==kind&&Math.Abs(N(c[2])-dn)<.01&&Math.Abs(N(c[3])-dn2)<.01);}
  static double Diameter(Pipe p){return Mm(p.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble());}
  static List<Connector> Peers(Connector c){return c.AllRefs.Cast<Connector>().Where(p=>p.Owner.Id!=c.Owner.Id&&p.ConnectorType==ConnectorType.End&&p.Domain==Domain.DomainPiping).ToList();}
  public static void CheckJoined(Connector c){var peers=Peers(c);if(!c.IsConnected||peers.Count!=1)throw new Exception("Đầu nối chưa có đúng một kết nối vật lý.");var p=peers[0];if(c.Origin.DistanceTo(p.Origin)>M(.1)||Math.Abs(c.Radius-p.Radius)>M(.01)||c.CoordinateSystem.BasisZ.DotProduct(p.CoordinateSystem.BasisZ)>-.999)throw new Exception("Connector không trùng vị trí/cỡ/hướng.");}
  sealed class Preserved {public Element Owner;public XYZ Origin;public long Peer;}
  static List<Preserved> Snapshot(Element e){return Ports(e).SelectMany(c=>Peers(c).Select(p=>new Preserved{Owner=p.Owner,Origin=p.Origin,Peer=e.Id.Value})).ToList();}
  static void PreserveCheck(IEnumerable<Preserved> ports,long splitId,long newHalf){foreach(var old in ports){var c=Ports(old.Owner).OrderBy(p=>p.Origin.DistanceTo(old.Origin)).FirstOrDefault();if(c==null||c.Origin.DistanceTo(old.Origin)>M(.1)||!c.IsConnected)throw new Exception("Kết nối cũ bị thay đổi; hoàn tác tuyến.");CheckJoined(c);var peer=Peers(c).Single().Owner.Id.Value;if(peer!=old.Peer&&!(old.Peer==splitId&&peer==newHalf))throw new Exception("Mạng cũ bị nối sang phần tử khác; hoàn tác.");}}
  static bool Overlap(Solid a,Solid b){var pa=Corners(a.GetBoundingBox(),Transform.Identity);var pb=Corners(b.GetBoundingBox(),Transform.Identity);return pa.Max(p=>p.X)>pb.Min(p=>p.X)+1e-8&&pb.Max(p=>p.X)>pa.Min(p=>p.X)+1e-8&&pa.Max(p=>p.Y)>pb.Min(p=>p.Y)+1e-8&&pb.Max(p=>p.Y)>pa.Min(p=>p.Y)+1e-8&&pa.Max(p=>p.Z)>pb.Min(p=>p.Z)+1e-8&&pb.Max(p=>p.Z)>pa.Min(p=>p.Z)+1e-8;}
  static Solid Cylinder(XYZ a,XYZ b,double radius){XYZ axis=(b-a).Normalize(),x=Math.Abs(axis.Z)<.9?axis.CrossProduct(XYZ.BasisZ).Normalize():XYZ.BasisX,y=axis.CrossProduct(x);var loop=new CurveLoop();loop.Append(Arc.Create(a,radius,0,Math.PI,x,y));loop.Append(Arc.Create(a,radius,Math.PI,2*Math.PI,x,y));return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{loop},axis,a.DistanceTo(b));}
  static bool ExactClear(List<XYZ> route,List<Obstacle> obstacles,double radius){for(int i=0;i<route.Count-1;i++){var tube=Cylinder(route[i],route[i+1],radius);foreach(var o in obstacles)foreach(var s in o.Solids){if(Overlap(tube,s)&&BooleanOperationsUtils.ExecuteBooleanOperation(tube,s,BooleanOperationsType.Intersect).Volume>1e-9)return false;}}return true;}
  static List<Obstacle> OwnObstacle(Element e){var b=e.get_BoundingBox(null);return b==null?new List<Obstacle>():new List<Obstacle>{new Obstacle{Id=e.Id.Value,Bounds=b,Solids=Solids(e.get_Geometry(new Options{DetailLevel=ViewDetailLevel.Fine}),Transform.Identity).ToList()}};}
  static bool SelfClear(List<XYZ> p,double radius){for(int i=0;i<p.Count-1;i++)for(int j=i+2;j<p.Count-1;j++){var a=Cylinder(p[i],p[i+1],radius);var b=Cylinder(p[j],p[j+1],radius);if(Overlap(a,b)&&BooleanOperationsUtils.ExecuteBooleanOperation(a,b,BooleanOperationsType.Intersect).Volume>1e-9)return false;}return true;}
  static bool Continuous(Element source,Pipe main,HashSet<long> allowed){var todo=new Queue<Element>();var seen=new HashSet<long>();todo.Enqueue(source);while(todo.Count>0){var e=todo.Dequeue();if(!seen.Add(e.Id.Value))continue;if(e.Id==main.Id)return true;foreach(var c in Ports(e))foreach(var p in Peers(c))if(allowed.Contains(p.Owner.Id.Value))todo.Enqueue(p.Owner);}return false;}
  public static RouteReport Connect(Document d,Pipe main,Connector source,XYZ selectedPoint,RouteSettings settings){
   if(main==null||source==null||!source.Owner.Document.Equals(d)||!main.Document.Equals(d)||source.Owner.Id==main.Id)throw new Exception("Chọn hai đối tượng khác nhau trong dự án hiện tại.");
   if(source.IsConnected||!IsSupply(source)||source.Shape!=ConnectorProfileType.Round)throw new Exception("Chọn connector cấp nóng/lạnh tròn còn trống.");
   if(!IsBmp(main))throw new Exception("Hãy chọn Pipe Type Bình Minh (BMP). Không tự đổi vật liệu ống hiện có.");
   var line=((LocationCurve)main.Location).Curve as Line;if(line==null)throw new Exception("Ống chính phải là đoạn thẳng; hỗ trợ mọi hướng trong 3D.");
   var type=(PipeType)d.GetElement(main.GetTypeId());string material=Library.Material(type),series=Library.Series(type);double dn=Mm(source.Radius*2),mainDn=Diameter(main);
   var sourcePipe=source.Owner as Pipe;if(sourcePipe!=null&&(!IsBmp(sourcePipe)||Library.Material((PipeType)d.GetElement(sourcePipe.GetTypeId()))!=material))throw new Exception("Nối khác vật liệu cần bộ chuyển được chỉ định. Không nối trực tiếp PVC sang PPR.");
   var classification=source.PipeSystemType==PipeSystemType.DomesticHotWater?MEPSystemClassification.DomesticHotWater:MEPSystemClassification.DomesticColdWater;
   if(material=="PVC"&&classification==MEPSystemClassification.DomesticHotWater)throw new Exception("PVC-U trong bộ này không dùng cấp nóng. Chọn PPR PN20 phù hợp.");
   if(classification==MEPSystemClassification.DomesticHotWater&&series!="PPR20")throw new Exception("Cấp nóng dùng Pipe Type BMP PPR PN20 trong bộ này.");
   var mainSystem=(PipingSystemType)d.GetElement(main.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId());
   bool change=mainSystem.SystemClassification!=classification;if(change&&Ports(main).Any(c=>c.IsConnected))throw new Exception("Ống chính đang nối vào hệ khác. Không đổi hệ một mạng hiện hữu; hãy chọn đúng ống cấp nóng/lạnh.");
   if(!File.ReadAllLines(Path.Combine(LibraryRoot,"catalogue","pipes.csv")).Skip(1).Select(x=>x.Split(',')).Any(c=>c[0]==series&&Math.Abs(N(c[1])-dn)<.01))throw new Exception("Không có cỡ ống "+dn.ToString("0.##")+" mm trong "+series+". Cần family bộ chuyển đúng catalogue ở đầu thiết bị.");
   var rows=Catalogue();ActualDimensions(d,rows);string junction=settings.Endpoint?(Math.Abs(dn-mainDn)<.01?"Socket":"Reducer"):(Math.Abs(dn-mainDn)<.01?"Tee":"ReducingTee");
   var part=Part(rows,material,junction,settings.Endpoint?Math.Max(dn,mainDn):mainDn,settings.Endpoint?Math.Min(dn,mainDn):dn);if(part==null)throw new Exception("Thiếu phụ kiện "+material+" "+junction+" "+mainDn+"×"+dn+" mm trong catalogue. Không tạo phụ kiện giả.");
   var elbow=Part(rows,material,"Elbow90",dn,dn);if(elbow==null)throw new Exception("Không có co 90° đúng cỡ trong bộ family.");
   var level=main.ReferenceLevel;if(level==null)throw new Exception("Ống chính thiếu tầng tham chiếu.");
   XYZ start=source.Origin,u=source.CoordinateSystem.BasisZ.Normalize(),at;Connector targetPort=null;
   double takeoff=rows.Where(c=>c[0]==material&&Math.Abs(N(c[2])-dn)<.01&&(c[1]=="Elbow90"||(settings.Use45&&c[1]=="Elbow45"))).Select(c=>Math.Max(N(c[7]),N(c[8]))).Max(),endTake=settings.Endpoint?N(part[11])/2:N(part[8]);
   if(settings.Endpoint){targetPort=Ports(main).Where(c=>!c.IsConnected).OrderBy(c=>c.Origin.DistanceTo(selectedPoint)).FirstOrDefault();if(targetPort==null)throw new Exception("Ống đích không có đầu còn trống.");at=targetPort.Origin;}else{at=line.Project(selectedPoint).XYZPoint;double dist=(at-line.GetEndPoint(0)).DotProduct(line.Direction);double margin=N(part[7])+20;if(dist<M(margin)||dist>line.Length-M(margin))throw new Exception("Điểm chèn tê quá sát đầu ống; chọn vị trí đủ chiều dài lắp phụ kiện.");}
   XYZ v=settings.Endpoint?-targetPort.CoordinateSystem.BasisZ.Normalize():u.CrossProduct(line.Direction);
   if(v.GetLength()<1e-6)v=u.CrossProduct(Math.Abs(u.Z)<.9?XYZ.BasisZ:XYZ.BasisX);v=v.Normalize();
   // Endpoint connections whose axes are skew require an intermediate 3D bend
   // frame. This implementation uses catalogue 45/90° only, never arbitrary elbows.
   bool skew=settings.Endpoint&&Math.Abs(u.DotProduct(v))>1e-5&&Math.Abs(Math.Abs(u.DotProduct(v))-1)>1e-5;
   XYZ y=Math.Abs(u.DotProduct(v))<.001?v:u.CrossProduct(Math.Abs(u.Z)<.9?XYZ.BasisZ:XYZ.BasisX).Normalize();var frame=new Frame{O=start,X=u,Y=y,Z=u.CrossProduct(y).Normalize()};
   int unloaded;var obstacles=Collect(d,new HashSet<long>{source.Owner.Id.Value,main.Id.Value},out unloaded);
   var ownSource=OwnObstacle(source.Owner);var ownTarget=OwnObstacle(main);
   double envelope=rows.Where(c=>c[0]==material&&Math.Abs(N(c[2])-dn)<.01&&(c[1]=="Elbow90"||c[1]=="Elbow45")).Select(c=>Math.Max(N(c[5]),N(c[6]))/2).DefaultIfEmpty(dn/2).Max();
   var options=new PlanOptions{Radius=envelope+settings.ClearanceMm,Padding=settings.SearchMm,MinLeg=takeoff*2+20,Chamfer=settings.Use45&&Part(rows,material,"Elbow45",dn,dn)!=null};
   var boxes=obstacles.Select(o=>LocalBox(o,frame)).ToList();PlanResult plan=null;List<XYZ> route=null;int states=0;var budget=Stopwatch.StartNew();
   XYZ direct=at-start;if(direct.GetLength()>M(endTake+10)&&direct.Normalize().DotProduct(u)>.999999&&(settings.Endpoint?direct.Normalize().DotProduct(v)>.999999:Math.Abs(u.DotProduct(line.Direction))<1e-6)&&settings.Guides.Count==0){var straight=new List<XYZ>{start,at};if(ExactClear(straight,obstacles,M(envelope+settings.ClearanceMm)))route=straight;}
   double aligned=(at-start).DotProduct(u)*304.8;int preferred=(at-start).DotProduct(v)<0?-1:1;
   foreach(double launch in new[]{aligned>=takeoff+25?aligned:takeoff+25,takeoff+25,150.0,300.0,500.0,750.0}.Distinct()){foreach(int sign in settings.Endpoint?new[]{1}:new[]{preferred,-preferred}){
    if(route!=null)break;
    if(budget.ElapsedMilliseconds>20000)break;
    XYZ approach=v*sign;double stub=takeoff+endTake+25;XYZ a=start+u*M(Math.Max(settings.LaunchMm,Math.Max(launch,takeoff+25))),b=at-approach*M(stub);
    if(skew){
     XYZ n=u.CrossProduct(v).Normalize();var left=new Frame{O=start,X=u,Y=n,Z=u.CrossProduct(n).Normalize()};
     var right=new Frame{O=start,X=n,Y=v,Z=n.CrossProduct(v).Normalize()};
     foreach(double shift in new[]{0.0,500.0,-500.0,1000.0,-1000.0}){
      if(budget.ElapsedMilliseconds>20000)break;
      XYZ center=(a+b)*.5+n*M(shift),c=center-n*M(options.MinLeg),e=center+n*M(options.MinLeg);
      var prefix=Planner.Find(left.Local(a),left.Local(c),new V(1,0,0),new V(0,1,0),obstacles.Select(o=>LocalBox(o,left)).ToList(),settings.Guides.Select(left.Local).ToList(),options,(p,q)=>ExactClear(new List<XYZ>{left.World(p),left.World(q)},obstacles,M(envelope+settings.ClearanceMm)));states+=prefix.Expanded;if(!prefix.Success){plan=prefix;continue;}
      var suffix=Planner.Find(right.Local(e),right.Local(b),new V(1,0,0),new V(0,1,0),obstacles.Select(o=>LocalBox(o,right)).ToList(),new V[0],options,(p,q)=>ExactClear(new List<XYZ>{right.World(p),right.World(q)},obstacles,M(envelope+settings.ClearanceMm)));states+=suffix.Expanded;if(!suffix.Success){plan=suffix;continue;}
      var candidatePoints=new[]{start}.Concat(prefix.Points.Select(left.World)).Concat(new[]{e}).Concat(suffix.Points.Skip(1).Select(right.World)).Concat(new[]{at}).ToList();
      candidatePoints=Planner.Simplify(candidatePoints.Select(left.Local).ToList()).Select(left.World).ToList();
      if(!SelfClear(candidatePoints,M(envelope))||!ExactClear(candidatePoints,obstacles,M(envelope+settings.ClearanceMm))||!ExactClear(candidatePoints.Skip(1).ToList(),ownSource,M(envelope))||!ExactClear(candidatePoints.Take(candidatePoints.Count-1).ToList(),ownTarget,M(envelope)))continue;route=candidatePoints;plan=suffix;break;
     }
     if(route!=null)break;continue;
    }
    var candidate=Planner.Find(frame.Local(a),frame.Local(b),new V(1,0,0),frame.Local(start+approach),boxes,settings.Guides.Select(frame.Local).ToList(),options,(p,q)=>ExactClear(new List<XYZ>{frame.World(p),frame.World(q)},obstacles,M(envelope+settings.ClearanceMm)));states+=candidate.Expanded;
    if(!candidate.Success){plan=candidate;continue;}var points=new[]{start}.Concat(candidate.Points.Select(frame.World)).Concat(new[]{at}).ToList();
    points=Planner.Simplify(points.Select(frame.Local).ToList()).Select(frame.World).ToList();
    if(!SelfClear(points,M(envelope))||!ExactClear(points,obstacles,M(envelope+settings.ClearanceMm))||!ExactClear(points.Skip(1).ToList(),ownSource,M(envelope))||!ExactClear(points.Take(points.Count-1).ToList(),ownTarget,M(envelope)))continue;route=points;plan=candidate;break;
   }if(route!=null||budget.ElapsedMilliseconds>20000)break;}
   if(route==null)throw new Exception((plan==null?"Không đủ khoảng trống để lắp phụ kiện.":plan.Reason)+" Có thể thêm điểm dẫn hoặc tăng phạm vi tìm kiếm.");
   Directory.CreateDirectory(Path.Combine(Root,"trace"));File.WriteAllText(Path.Combine(Root,"trace","planned-"+source.Owner.Id.Value+".json"),new JavaScriptSerializer().Serialize(route.Select(p=>new[]{Mm(p.X),Mm(p.Y),Mm(p.Z)}).ToArray()));
   var sourceId=source.Owner.Id;var preserved=Snapshot(main).Concat(Snapshot(source.Owner)).ToList();var newPipes=new List<Pipe>();var fits=new List<FamilyInstance>();Pipe second=null;var created=new HashSet<long>();
   using(var group=new TransactionGroup(d,"KMep — Đấu nối ống cấp 3D")){
    group.Start();try{
     using(var t=new Transaction(d,"Nạp phụ kiện đúng catalogue")){t.Start();foreach(string kind in new[]{"Elbow90","Elbow45",junction}.Distinct()){string name="BMP_"+material+"_"+kind;if(new FilteredElementCollector(d).OfClass(typeof(Family)).Cast<Family>().Any(f=>f.Name==name))continue;Family family;if(!d.LoadFamily(Path.Combine(LibraryRoot,"output","FamiliesParametric",name+".rfa"),out family))throw new Exception("Không nạp được "+name);}if(t.Commit()!=TransactionStatus.Committed)throw new Exception("Không nạp được family.");}
     using(var t=new Transaction(d,"Tạo ống và kết nối phụ kiện")){
      t.Start();var failures=new RouteFailures();var handling=t.GetFailureHandlingOptions();handling.SetFailuresPreprocessor(failures);handling.SetClearAfterRollback(true);t.SetFailureHandlingOptions(handling);
      var system=new FilteredElementCollector(d).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().FirstOrDefault(s=>s.SystemClassification==classification);if(system==null)throw new Exception("Dự án thiếu hệ cấp nóng/lạnh tương ứng.");
      if(change)main.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).Set(system.Id);else system=(PipingSystemType)d.GetElement(main.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId());d.Regenerate();
      var first=Pipe.Create(d,type.Id,level.Id,source,route[1]);newPipes.Add(first);d.Regenerate();if(Math.Abs(Diameter(first)-dn)>.01)throw new Exception("Revit thay đổi cỡ nhánh tại thiết bị.");
      var actualSystem=first.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId();if(actualSystem!=system.Id){if(!change)throw new Exception("Hai đầu thuộc hai System Type khác nhau; không tự gộp mạng.");main.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).Set(actualSystem);}
      var network=new BoundedNetwork(d,LibraryRoot,new[]{main.Id,first.Id});
      for(int i=1;i<route.Count-1;i++){var p=network.CreatePipe(actualSystem,type.Id,level.Id,dn,route[i],route[i+1]);try{fits.Add(network.Connect(new List<Pipe>{newPipes.Last(),p},route[i],material));}catch(Exception ex){throw new Exception("Không lắp được co thứ "+i+": "+ex.Message,ex);}newPipes.Add(p);}
      if(settings.Endpoint)fits.Add(network.Connect(new List<Pipe>{main,newPipes.Last()},at,material));else{second=network.SplitSelectedPipe(main,at);fits.Add(network.Connect(new List<Pipe>{main,second,newPipes.Last()},at,material));}
      d.Regenerate();foreach(var p in newPipes)foreach(var c in Ports(p))CheckJoined(c);foreach(var f in fits)foreach(var c in Ports(f))CheckJoined(c);CheckJoined(Ports(d.GetElement(sourceId)).OrderBy(c=>c.Origin.DistanceTo(start)).First());PreserveCheck(preserved,main.Id.Value,second==null?-1:second.Id.Value);
      foreach(var p in newPipes.Concat(second==null?new[]{main}:new[]{main,second})){var st=(PipingSystemType)d.GetElement(p.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId());if(st.SystemClassification!=classification)throw new Exception("Một đoạn ống sai hệ cấp nước.");}
      foreach(var p in newPipes)created.Add(p.Id.Value);foreach(var f in fits)created.Add(f.Id.Value);created.Add(main.Id.Value);if(second!=null)created.Add(second.Id.Value);created.Add(sourceId.Value);
      if(!Continuous(d.GetElement(sourceId),main,created))throw new Exception("Không có đường kết nối liên tục tới ống chính.");
      // Check actual fitting bodies as well as planning clearance envelopes.
      foreach(var element in newPipes.Cast<Element>().Concat(fits))foreach(var solid in Solids(element.get_Geometry(new Options{DetailLevel=ViewDetailLevel.Fine}),Transform.Identity))foreach(var obstacle in obstacles)foreach(var other in obstacle.Solids)if(Overlap(solid,other)&&BooleanOperationsUtils.ExecuteBooleanOperation(solid,other,BooleanOperationsType.Intersect).Volume>1e-9)throw new Exception("Thân ống/phụ kiện va chạm phần tử "+obstacle.Id+"; hoàn tác.");
      foreach(var fitting in fits)foreach(var solid in Solids(fitting.get_Geometry(new Options{DetailLevel=ViewDetailLevel.Fine}),Transform.Identity))foreach(var own in ownSource.Concat(fitting.Id==fits.Last().Id?new List<Obstacle>():ownTarget))foreach(var other in own.Solids)if(Overlap(solid,other)&&BooleanOperationsUtils.ExecuteBooleanOperation(solid,other,BooleanOperationsType.Intersect).Volume>1e-9)throw new Exception("Phụ kiện chạm vào thân thiết bị hoặc ống được chọn; hãy tăng đoạn thoát đầu thiết bị.");
      if(t.Commit()!=TransactionStatus.Committed)throw new Exception(failures.Error??"Revit hoàn tác giao dịch.");
     }
     PreserveCheck(preserved,main.Id.Value,second==null?-1:second.Id.Value);group.Assimilate();
    }catch{if(group.GetStatus()==TransactionStatus.Started)group.RollBack();throw;}
   }
   return new RouteReport{Status="PASS",Document=d.PathName,Source=sourceId.Value,Target=main.Id.Value,Material=material,System=classification.ToString(),BranchMm=dn,MainMm=mainDn,LengthMm=route.Zip(route.Skip(1),(a,b)=>Mm(a.DistanceTo(b))).Sum(),SearchStates=states,UnloadedLinks=unloaded,Created=created.Where(id=>id!=sourceId.Value&&id!=main.Id.Value).ToArray(),Fittings=fits.Select(f=>f.Symbol.Family.Name+" : "+f.Symbol.Name).ToArray(),RouteMm=route.Select(p=>new[]{Mm(p.X),Mm(p.Y),Mm(p.Z)}).ToArray(),Reason="Connector, continuity, systems, existing connections and modeled obstacle geometry checked."};
  }
  public static void SaveReport(RouteReport r){string folder=Path.Combine(Root,"results");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"Route_"+DateTime.Now.ToString("yyyyMMdd_HHmmssfff")+".json"),new JavaScriptSerializer().Serialize(r));}
  sealed class RouteFailures:IFailuresPreprocessor {public string Error;public FailureProcessingResult PreprocessFailures(FailuresAccessor a){foreach(var f in a.GetFailureMessages()){Error=f.GetDescriptionText();return FailureProcessingResult.ProceedWithRollBack;}return FailureProcessingResult.Continue;}}
 }
}

