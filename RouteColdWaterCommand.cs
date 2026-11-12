using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace KMep.PlumbingPro
{
    [Transaction(TransactionMode.Manual)]
    public sealed class RouteColdWaterCommand : IExternalCommand
    {
        private const double MmPerFoot = 304.8;
        private static double Mm(double value) { return value / MmPerFoot; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) { message = "Cần mở dự án Revit."; return Result.Failed; }
            Document doc = uidoc.Document;
            try
            {
                TaskDialog.Show("KMep · PlumbingPro", "Bản v0.1: chọn một ống CHÍNH cấp nước lạnh đã vẽ, sau đó chọn MỘT thiết bị vệ sinh có connector cấp lạnh.\n\n" +
                    "Chỉ tạo ống nhánh thẳng thử nghiệm, KHÔNG tự chèn tê hoặc đấu nối ống chính.\n" +
                    "Đường kính lấy từ connector thiết bị (chưa tính thủy lực). Độ dốc 2% không áp dụng cho cấp nước lạnh.");
                Reference mainRef = uidoc.Selection.PickObject(ObjectType.Element, new MainPipeFilter(), "Chọn ống cấp nước lạnh chính");
                Pipe main = doc.GetElement(mainRef) as Pipe;
                Reference fixtureRef = uidoc.Selection.PickObject(ObjectType.Element, new FixtureFilter(), "Chọn thiết bị có đầu nối nước lạnh");
                FamilyInstance fixture = doc.GetElement(fixtureRef) as FamilyInstance;
                if (main == null || fixture == null) throw new InvalidOperationException("Lựa chọn không hợp lệ.");

                Connector source = FindColdWaterConnector(fixture);
                if (source == null) throw new InvalidOperationException("Family không có connector cấp lạnh Piping chưa kết nối. Hãy cấu hình connector đúng hệ Domestic Cold Water.");
                LocationCurve mainLocation = main.Location as LocationCurve;
                Line mainLine = mainLocation == null ? null : mainLocation.Curve as Line;
                if (mainLine == null) throw new InvalidOperationException("v0.1 chỉ hỗ trợ ống chính dạng thẳng.");
                XYZ endpoint = ProjectToSegment(source.Origin, mainLine);
                if (source.Origin.DistanceTo(endpoint) < Mm(50)) throw new InvalidOperationException("Khoảng cách tới ống chính dưới 50 mm, không đủ tạo đoạn nhánh thử nghiệm.");

                // A straight branch is possible only if endpoint is close to the source connector's axis.
                // Otherwise creating Pipe from a connector may fail or create undesired fittings.
                XYZ axis = source.CoordinateSystem.BasisZ.Normalize();
                XYZ dir = (endpoint - source.Origin).Normalize();
                if (Math.Abs(axis.DotProduct(dir)) < 0.98)
                    throw new InvalidOperationException("Đầu nối không hướng về ống chính. Bản đầu tiên chưa có thuật toán đi ống gấp khúc; hãy đổi hướng family hoặc chờ router nhiều đoạn.");
                if (endpoint.DistanceTo(mainLine.GetEndPoint(0)) < Mm(100) || endpoint.DistanceTo(mainLine.GetEndPoint(1)) < Mm(100))
                    throw new InvalidOperationException("Điểm chiếu nằm quá gần đầu ống chính (dưới 100 mm).");

                ElementId levelId = main.ReferenceLevel == null ? ElementId.InvalidElementId : main.ReferenceLevel.Id;
                if (levelId == ElementId.InvalidElementId) throw new InvalidOperationException("Ống chính chưa có Reference Level hợp lệ.");
                IList<ElementId> collisionIds = FindPotentialCollisions(doc, source.Origin, endpoint, fixture.Id, main.Id);
                if (collisionIds.Count > 0)
                {
                    TaskDialog.Show("KMep · Kiểm tra va chạm", "Không tạo ống vì hành lang tuyến nhánh có khả năng chạm kết cấu/kiến trúc.\nElementId: " + string.Join(", ", collisionIds.Take(12).Select(x => x.IntegerValue.ToString())) + "\n\nKiểm tra các phần tử này trong mô hình. Phép thử dùng Bounding Box (có thể báo nhầm).");
                    return Result.Cancelled;
                }

                Pipe created;
                using (Transaction tx = new Transaction(doc, "KMep | Tạo ống nhánh cấp lạnh"))
                {
                    tx.Start();
                    // Revit chooses system and nominal diameter from the fixture's connector.
                    created = Pipe.Create(doc, main.GetTypeId(), levelId, source, endpoint);
                    doc.Regenerate();
                    // The pipe stops at the centerline of the main and is NOT connected to it.
                    // Keep the connection to the fixture, avoid modifying existing main pipe.
                    tx.Commit();
                }
                TaskDialog.Show("KMep · PlumbingPro", "Đã tạo đoạn ống nhánh thử nghiệm.\nPipe Id: " + created.Id.IntegerValue +
                    "\nĐường kính connector: " + (source.Radius * 2 * MmPerFoot).ToString("0.#") + " mm" +
                    "\n\nCHƯA tạo tê và chưa kết nối thủy lực vào ống chính. Kiểm tra trong 3D trước khi sử dụng.");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex) { message = ex.Message; TaskDialog.Show("KMep · Lỗi", ex.Message); return Result.Failed; }
        }

        private static Connector FindColdWaterConnector(FamilyInstance fixture)
        {
            if (fixture.MEPModel == null || fixture.MEPModel.ConnectorManager == null) return null;
            foreach (Connector c in fixture.MEPModel.ConnectorManager.Connectors)
                if (c.Domain == Domain.DomainPiping && !c.IsConnected && c.PipeSystemType == PipeSystemType.DomesticColdWater)
                    return c;
            return null;
        }
        private static XYZ ProjectToSegment(XYZ point, Line line)
        {
            XYZ a = line.GetEndPoint(0), b = line.GetEndPoint(1), ab = b - a;
            double t = (point - a).DotProduct(ab) / ab.DotProduct(ab);
            t = Math.Max(0, Math.Min(1, t));
            return a + t * ab;
        }
        private static IList<ElementId> FindPotentialCollisions(Document doc, XYZ a, XYZ b, ElementId fixtureId, ElementId mainPipeId)
        {
            // Conservative broad-phase safety check; NOT exact solid-clash testing.
            double padding = Mm(35);
            XYZ lo = new XYZ(Math.Min(a.X, b.X) - padding, Math.Min(a.Y, b.Y) - padding, Math.Min(a.Z, b.Z) - padding);
            XYZ hi = new XYZ(Math.Max(a.X, b.X) + padding, Math.Max(a.Y, b.Y) + padding, Math.Max(a.Z, b.Z) + padding);
            var outline = new Outline(lo, hi);
            var ids = new List<ElementId>();
            var categories = new[] { BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralColumns };
            var filter = new LogicalOrFilter(categories.Select(c => (ElementFilter)new ElementCategoryFilter(c)).ToList());
            foreach (Element element in new FilteredElementCollector(doc).WherePasses(filter)
                        .WherePasses(new BoundingBoxIntersectsFilter(outline)).WhereElementIsNotElementType())
            {
                if (element.Id != fixtureId && element.Id != mainPipeId) ids.Add(element.Id);
            }
            return ids;
        }
        private sealed class MainPipeFilter : ISelectionFilter
        {
            public bool AllowElement(Element e)
            {
                Pipe pipe = e as Pipe;
                if (pipe == null) return false;
                ConnectorManager cm = pipe.ConnectorManager;
                return cm != null && cm.Connectors.Cast<Connector>()
                    .Any(c => c.Domain == Domain.DomainPiping && c.PipeSystemType == PipeSystemType.DomesticColdWater);
            }
            public bool AllowReference(Reference r, XYZ p) { return false; }
        }
        private sealed class FixtureFilter : ISelectionFilter
        {
            public bool AllowElement(Element e) { return e is FamilyInstance fi && FindColdWaterConnector(fi) != null; }
            public bool AllowReference(Reference r, XYZ p) { return false; }
        }
    }
}
