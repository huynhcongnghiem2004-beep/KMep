# KMep — Đấu nối ống cấp 3D — Revit 2024, mm

Nút **KMep → PlumbingPro → Đấu nối / Ống cấp 3D** thay lệnh đấu lavabo cố định trước đây.

## Dùng lệnh

1. Chọn đúng vị trí muốn chèn tê trên một ống Bình Minh. Nếu định nối đầu, chọn gần đầu trống đó.
2. Chọn thiết bị, phụ kiện có connector cấp nước hoặc một đầu ống cấp còn trống.
3. Chọn connector cấp lạnh/cấp nóng trong hộp thiết lập. Đánh dấu **Nối vào đầu trống** nếu không muốn chèn tê.
4. Chọn khoảng hở, phạm vi tìm đường, đoạn thoát đầu thiết bị và việc dùng co 45°. Có thể chọn thêm tối đa 8 điểm dẫn trên work plane đang hoạt động; Esc kết thúc chọn điểm và bắt đầu tìm đường.
5. Bấm **Tìm và đấu nối**. Lệnh chọn các phần tử mới để kiểm tra. **Ctrl+S** lưu dự án. Một lần Undo hoàn tác nhóm đấu nối.

## Phạm vi

- PVC-U cấp lạnh, PPR PN10/PN20 cấp lạnh và PPR PN20 cấp nóng, theo cỡ và tổ hợp thật trong catalogue của thư viện đã có.
- Đấu thiết bị vào giữa đoạn ống bằng tê/tê giảm hoặc vào đầu trống bằng nối/giảm cỡ. Giữ cỡ đầu thiết bị, không tự tính hoặc giảm cỡ theo lưu lượng.
- Ống chính thẳng đứng, ngang, nghiêng và các hướng connector trong không gian. Hai đầu lệch hướng có thể dùng hai khung tìm đường 3D với các đoạn chuyển hướng 45°/90°.
- Tìm tuyến vòng vật cản, thay hướng tiếp cận tê và chiều dài đoạn thoát. Giữ kết nối sẵn có ở các đầu xa của ống chính/ống nguồn.
- Kiểm tra hình khối mô hình hiện tại và Revit Link đã nạp, kể cả link xoay và link lồng. Link chưa nạp được ghi rõ là chưa kiểm tra.
- Đọc kích thước family Type đang có trong dự án để chừa chiều dài lắp, kể cả khi thông số Type đã được chỉnh.

## Kiểm tra và giới hạn

Bộ tìm đường có giới hạn thời gian, số trạng thái và phạm vi. Không thể bảo đảm tìm được mọi tuyến khả thi trong một mô hình bất kỳ. Khi quá chật, hãy tăng đoạn thoát, chọn vị trí tê khác, thêm điểm dẫn hoặc tăng phạm vi. Khoảng hở tính quanh thân ống/co theo cỡ phụ kiện; sau khi tạo, hình khối thân ống và phụ kiện còn được kiểm tra giao nhau với vật cản. Nhánh không được tự xuyên tường/sàn; cần mô hình lỗ mở hợp lệ nếu muốn đi qua.

Các trường hợp thiếu cỡ ống/phụ kiện đúng catalogue, nối PVC sang PPR, connector PPR Ø21 nhưng thư viện chỉ có Ø20, nhánh lớn hơn ống chính mà không có tổ hợp tê tương ứng, sai hệ trên mạng đang nối hoặc không đủ chiều dài lắp đều bị từ chối. Không tự tạo bộ chuyển giả, không thay đổi cả mạng sang hệ khác. PVC-U cấp nóng và PPR PN10 cấp nóng bị từ chối trong bộ này.

Đoạn ống đầu tiên được phép thoát từ connector của chính thiết bị; các đoạn sau kiểm tra với thân thiết bị. Vùng tiếp xúc tại tê/nối với ống đích được xử lý riêng. Chưa kiểm tra vỏ cách nhiệt, khoảng bảo trì van, hình học chỉ có mesh/đường hoặc kiểm tra thủy lực, áp lực/nhiệt độ thiết kế. Hình học không xử lý được sẽ báo lỗi thay vì xác nhận không va chạm. Co chỉ dùng góc thật 45°/90° trong bộ family. Chưa có chế độ chọn nhiều thiết bị và đấu hàng loạt trong một lệnh.

Một giao dịch nhóm chứa nạp family, tạo ống, tách ống chính, lắp phụ kiện và kiểm tra. Lỗi tạo tuyến sẽ hoàn tác nhóm. Kiểm tra gồm vị trí/cỡ/hướng và kết nối vật lý của connector; đường đi liên tục tới ống chính; đúng hệ cấp nước; giữ nguyên các đầu nối cũ. Báo cáo JSON nằm trong `results`. `trace` chứa tim tuyến dự kiến để chẩn đoán.

## Build, cài đặt và phụ thuộc

- .NET Framework 4.8, Revit 2024. Đơn vị giao diện và catalogue: mm.
- Build bằng `build.ps1` sau khi đóng Revit nếu DLL đích đang được nạp. Có thể truyền `-OutputName` để build tên khác.
- DLL chính: `KMep.SupplyRouter2024.Final2.dll`; lớp Application: `KMep.Supply.App`.
- Phụ thuộc thư mục `../BinhMinhLibrary/BinhMinh_Parametric_Revit2024_mm`, gồm DLL, catalogue và family nhiều Type. Giữ nguyên quan hệ hai thư mục này.
- `Install-Revit2024.ps1` sao lưu và cập nhật manifest KMep có AddInId cũ, giữ nguyên các add-in khác. Manifest cũ trong thư mục bản cập nhật có thể dùng để phục hồi.

## Bằng chứng kiểm thử

Build Final ngày 28/10/2026 đạt **124/124 ca Revit**, gồm 38 ca hình học/hệ thống/hoàn tác và 86 tổ hợp tê, tê giảm, nối thẳng, giảm cỡ trong catalogue. Đây là phạm vi đã kiểm chứng, không phải chứng nhận xử lý được mọi mô hình hoặc mọi vị trí thực tế.

`PlannerTests.cs` chạy độc lập với Revit: 1.022 xác nhận về hướng, góc, tránh vật cản, điểm dẫn, vị trí ngẫu nhiên và điều kiện dừng.

`IntegrationTests.cs` chạy trong Revit trên tài liệu thử riêng, không sửa tài liệu đang làm việc. Bao gồm fixture lavabo/bồn cầu thật, mọi hướng trục, ống chính đã có kết nối, PPR nóng/lạnh, vật cản trong link xoay, chỉnh Type, các trường hợp phải từ chối và tổ hợp catalogue. Mỗi ca được hoàn tác rồi kiểm tra số phần tử phục hồi. Xem báo cáo của bản Final trong `tests-KMep.SupplyRouter2024.Final2/integration-results.json` và `integration-summary.txt` để biết kết quả thực tế của build này.


