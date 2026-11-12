using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using F=System.Windows.Forms;
namespace KMep.Supply {
 public sealed class App:IExternalApplication {
  static App(){AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{if(new AssemblyName(e.Name).Name!="BinhMinh.Parametric2024")return null;return Assembly.LoadFrom(Path.Combine(Router.LibraryRoot,"BinhMinh.Parametric2024.dll"));};}
  public Result OnStartup(UIControlledApplication a){try{a.CreateRibbonTab("KMep");}catch(Autodesk.Revit.Exceptions.ArgumentException){}SetButton(a.GetRibbonPanels("KMep").FirstOrDefault(p=>p.Name=="PlumbingPro")??a.CreateRibbonPanel("KMep","PlumbingPro"));return Result.Succeeded;}
  public static void AddToSession(UIApplication a){SetButton(a.GetRibbonPanels("KMep").First(p=>p.Name=="PlumbingPro"));}
  static void SetButton(RibbonPanel p){var b=p.GetItems().OfType<PushButton>().FirstOrDefault(x=>x.Name=="KMep.RouteColdWater");if(b==null)b=(PushButton)p.AddItem(new PushButtonData("KMep.RouteColdWater","Đấu nối\nỐng cấp 3D",typeof(App).Assembly.Location,typeof(ConnectCommand).FullName));b.AssemblyName=typeof(App).Assembly.Location;b.ClassName=typeof(ConnectCommand).FullName;b.ItemText="Đấu nối\nỐng cấp 3D";b.ToolTip="Đấu thiết bị hoặc đầu ống vào ống chính Bình Minh PVC/PPR; cấp nóng/lạnh, chèn tê hoặc nối đầu, tìm đường 3D tránh vật cản và kiểm tra kết nối.";}
  public Result OnShutdown(UIControlledApplication a){return Result.Succeeded;}
 }
 sealed class SettingsForm:F.Form {
  readonly F.ComboBox ports=new F.ComboBox{DropDownStyle=F.ComboBoxStyle.DropDownList,Width=445};
  readonly F.CheckBox endpoint=new F.CheckBox{Text="Nối vào đầu trống gần điểm chọn (bỏ chọn = chèn tê)",Width=445};
  readonly F.CheckBox diagonal=new F.CheckBox{Text="Cho phép co 45° theo catalogue",Checked=true,Width=445};
  readonly F.CheckBox guides=new F.CheckBox{Text="Chọn thêm điểm dẫn trên mặt phẳng làm việc (Esc kết thúc)",Width=445};
  readonly F.NumericUpDown clearance=new F.NumericUpDown{Minimum=0,Maximum=500,Value=10,Width=100};
  readonly F.NumericUpDown launch=new F.NumericUpDown{Minimum=0,Maximum=2000,Value=0,Increment=50,Width=100};
  readonly F.NumericUpDown search=new F.NumericUpDown{Minimum=500,Maximum=10000,Value=2000,Increment=500,Width=100};
  readonly List<Connector> connectors;
  public Connector Source{get{return connectors[ports.SelectedIndex];}}public bool Guides{get{return guides.Checked;}}
  public RouteSettings Settings{get{return new RouteSettings{Endpoint=endpoint.Checked,Use45=diagonal.Checked,ClearanceMm=(double)clearance.Value,SearchMm=(double)search.Value,LaunchMm=(double)launch.Value};}}
  public SettingsForm(List<Connector> c){connectors=c;Text="KMep — Đấu nối ống cấp 3D";Width=520;Height=550;FormBorderStyle=F.FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=F.FormStartPosition.CenterScreen;var flow=new F.FlowLayoutPanel{Dock=F.DockStyle.Fill,AutoScroll=true,Padding=new F.Padding(15),FlowDirection=F.FlowDirection.TopDown,WrapContents=false};Controls.Add(flow);flow.Controls.Add(new F.Label{Text="Chọn đầu cấp nước cần nối:",AutoSize=true});foreach(var p in c)ports.Items.Add((p.PipeSystemType==PipeSystemType.DomesticHotWater?"Cấp nóng":"Cấp lạnh")+" — Ø"+(p.Radius*609.6).ToString("0.##")+" mm — Connector "+p.Id);ports.SelectedIndex=0;flow.Controls.Add(ports);flow.Controls.Add(endpoint);flow.Controls.Add(diagonal);flow.Controls.Add(guides);flow.Controls.Add(new F.Label{Text="Khoảng hở với vật cản (mm):",AutoSize=true});flow.Controls.Add(clearance);flow.Controls.Add(new F.Label{Text="Phạm vi tìm đường quanh hai đầu/điểm dẫn (mm):",AutoSize=true});flow.Controls.Add(search);flow.Controls.Add(new F.Label{Text="Đoạn thoát đầu thiết bị tối thiểu (mm; 0 = tự chọn):",AutoSize=true});flow.Controls.Add(launch);var buttons=new F.FlowLayoutPanel{Width=445,Height=40};var ok=new F.Button{Text="Tìm và đấu nối",Width=145,DialogResult=F.DialogResult.OK};var cancel=new F.Button{Text="Hủy",DialogResult=F.DialogResult.Cancel};buttons.Controls.Add(ok);buttons.Controls.Add(cancel);flow.Controls.Add(buttons);AcceptButton=ok;CancelButton=cancel;}
 }
 [Transaction(TransactionMode.Manual)]
 public sealed class ConnectCommand:IExternalCommand {
  public Result Execute(ExternalCommandData data,ref string message,ElementSet elements){
   bool connected=false;
   try{
    var ui=data.Application.ActiveUIDocument;if(ui==null||ui.Document.IsFamilyDocument)throw new Exception("Mở dự án MEP trước khi đấu nối.");var d=ui.Document;
    var target=ui.Selection.PickObject(ObjectType.Element,new MainFilter(),"KMep: chọn vị trí chèn tê trên ống chính, hoặc gần đầu ống muốn nối");var main=(Pipe)d.GetElement(target);
    var reference=ui.Selection.PickObject(ObjectType.Element,new SourceFilter(),"KMep: chọn thiết bị hoặc đầu ống cấp nóng/lạnh còn trống");var owner=d.GetElement(reference);
    var ports=Router.Ports(owner).Where(c=>Router.IsSupply(c)&&!c.IsConnected&&c.Shape==ConnectorProfileType.Round).OrderBy(c=>c.Origin.DistanceTo(reference.GlobalPoint)).ToList();if(ports.Count==0)throw new Exception("Không có connector cấp nóng/lạnh còn trống.");
    using(var form=new SettingsForm(ports)){
     if(form.ShowDialog()!=F.DialogResult.OK)return Result.Cancelled;var settings=form.Settings;
     if(form.Guides){for(int i=0;i<8;i++){try{settings.Guides.Add(ui.Selection.PickPoint(ObjectSnapTypes.Endpoints|ObjectSnapTypes.Nearest,"Chọn điểm dẫn "+(i+1)+" trên work plane; Esc để tìm đường"));}catch(Autodesk.Revit.Exceptions.OperationCanceledException){break;}}}
     var report=Router.Connect(d,main,form.Source,target.GlobalPoint,settings);connected=true;Router.SaveReport(report);
     var ids=report.Created.Select(id=>new ElementId(id)).ToList();ui.Selection.SetElementIds(ids);ui.ShowElements(ids);
     TaskDialog.Show("KMep — Đã đấu nối","Hệ: "+report.System+"\nVật liệu: "+report.Material+"; nhánh Ø"+report.BranchMm+" → chính Ø"+report.MainMm+" mm.\n"+report.Fittings.Length+" phụ kiện; chiều dài tim tuyến "+(report.LengthMm/1000).ToString("0.00")+" m.\nĐã kiểm tra connector, tuyến liên tục, kết nối cũ và va chạm mô hình."+(report.UnloadedLinks>0?"\nCó "+report.UnloadedLinks+" link chưa nạp, chưa kiểm tra được hình học các link này.":"")+"\nCtrl+S để lưu dự án.");
    }return Result.Succeeded;
   }catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}
   catch(Exception e){message=e.Message;TaskDialog.Show(connected?"KMep — Đã nối, lỗi hiển thị/báo cáo":"KMep — Chưa đấu nối",(connected?"Tuyến đã được tạo. Ctrl+S để lưu.\n":"")+e.Message);return connected?Result.Succeeded:Result.Cancelled;}
  }
  sealed class MainFilter:ISelectionFilter {public bool AllowElement(Element e){return e is Pipe&&Router.IsBmp((Pipe)e);}public bool AllowReference(Reference r,XYZ p){return false;}}
  sealed class SourceFilter:ISelectionFilter {public bool AllowElement(Element e){return Router.Ports(e).Any(c=>Router.IsSupply(c)&&!c.IsConnected);}public bool AllowReference(Reference r,XYZ p){return false;}}
 }
}

