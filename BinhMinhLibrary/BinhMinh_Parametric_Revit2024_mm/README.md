# Bình Minh — family nhiều Type, Revit 2024, mm

Bộ BIM dựng độc lập theo catalogue Bình Minh đã cung cấp; không phải thư viện do hãng phát hành. Hình học được đơn giản hóa để dùng trong mô hình MEP.

## Cấu trúc mới

Mỗi loại phụ kiện và vật liệu có đúng **một RFA**. Các cỡ được lưu thành **Family Types** trong file đó: tổng cộng **14 family, 131 Type**.

| Loại | PVC | PPR |
|---|---:|---:|
| Co 90° / Elbow90 | 9 Type | 6 Type |
| Co 45° / Elbow45 | 9 Type | 6 Type |
| Tê đều / Tee | 9 Type | 6 Type |
| Tê giảm / ReducingTee | 14 Type | 15 Type |
| Nối thẳng / Socket | 9 Type | 6 Type |
| Nối giảm / Reducer | 13 Type | 14 Type |
| Nắp bịt / Cap | 9 Type | 6 Type |

Ví dụ: `BMP_PVC_Elbow90.rfa` chứa các Type `21 mm`, `27 mm`, `34 mm`…; `BMP_PPR_ReducingTee.rfa` chứa `25x20 mm`, `32x20 mm`, `32x25 mm`…

Ống thẳng là System Family của Revit, được cung cấp qua Pipe Types và Pipe Segments trong dự án RVT/add-in. Không có file RFA cho ống thẳng. Pipe Types gồm PVC-U, PPR PN10 và PPR PN20; phụ kiện PPR theo bảng PN20.

## Chọn Type và sửa thông số

1. Trong dự án, chọn phụ kiện rồi chọn Type ở đầu bảng Properties.
2. Dùng **Edit Type → Duplicate** trước khi tùy chỉnh để giữ nguyên cỡ catalogue.
3. Sửa các thông số tương ứng với loại phụ kiện. Chúng là Type Parameters, đơn vị mm, điều khiển hình học Revit và connector.

| Thông số | Tác dụng |
|---|---|
| `BMP_DN_mm`, `BMP_DN2_mm` | Đường kính danh nghĩa của connector chính và đầu giảm/nhánh |
| `BMP_D_mm`, `BMP_D2_mm` | Đường kính ngoài thân chính và nhánh/đầu nhỏ |
| `BMP_Bore_mm`, `BMP_Bore2_mm` | Đường kính lỗ; lưu riêng với DN, ví dụ PVC Ø21 có OD 21,3 mm |
| `BMP_H_mm` | Từ giao điểm lý thuyết tới miệng nối của co; nửa chiều dài chính của tê |
| `BMP_H2_mm` | Chiều dài nhánh tê từ giao điểm tới miệng nối |
| `BMP_L_mm` | Tổng chiều dài nối thẳng, nối giảm hoặc nắp |
| `BMP_Z_mm` | Chiều dày đầu kín của nắp; ở nhóm khác là số liệu catalogue tham khảo |
| `BMP_Z2_mm` | Số liệu catalogue tham khảo, không điều khiển hình học ở bản đơn giản hóa này |
| `Angle` | Góc cố định theo nhóm family: 45° hoặc 90° |

Các thông số phụ Start/End/Radius được tính bằng công thức. H/H2/L chỉ có tác dụng ở các loại nêu trong bảng. Đổi Type cập nhật đầy đủ bộ kích thước catalogue. Khi sửa riêng DN của một Type tự tạo, cần sửa D/Bore/H/L phù hợp; không có bảng tra tự suy toàn bộ kích thước theo DN tùy ý.

Thay đổi một Type tác động đến mọi instance đang dùng Type đó. Add-in tự nối chọn các Type catalogue có sẵn; Type tự đặt tên có thể đặt thủ công. Các kích thước tùy chỉnh không được xem là mã sản phẩm đã được Bình Minh xác nhận.

## Cài và dùng bộ tự nối

1. Giải nén đầy đủ, giữ nguyên các thư mục.
2. Chạy `Install-Parametric-Revit2024.ps1` để đăng ký DLL mới, thay bản add-in Bình Minh một file/một cỡ cũ. Script lưu bản sao manifest cũ trong `previous`.
3. Mở lại Revit 2024; người dùng tự xử lý hộp thoại add-in chưa ký số nếu Revit hỏi.
4. Vào **KMep → Bình Minh Parametric → Nạp thư viện** trong dự án cần dùng.
5. Dùng **Vẽ ống Bình Minh**, **Vẽ nhánh Bình Minh**, **Nối hai đầu ống**, **Bịt đầu ống**.

Add-in chọn đúng Family + Type, đọc kích thước H/H2/L của Type để nối đúng vị trí. Việc sửa thông số Type không cần add-in để hiển thị hình học và connector. Bộ tự chọn đủ mã co 45°/90° và phụ kiện giảm cỡ khi vẽ sử dụng các lệnh BMP; công cụ Pipe gốc của Revit chưa được xác nhận thay thế toàn bộ các lệnh này.

Các lệnh chỉ sửa ống mới tạo hoặc ống bạn trực tiếp chọn. Không có updater chạy nền sửa các mạng ống khác. Khi gặp lỗi, thao tác nối được rollback. Phạm vi gồm co đúng 45°/90°, tê có nhánh 90° và nối giảm đồng trục; hệ thoát có độ dốc tùy ý chưa được xác nhận.

## File bàn giao

- `output/FamiliesParametric/`: 14 file RFA nhiều Type.
- `output/BMP_Parametric_Revit2024_mm.rvt`: dự án thư viện và các tuyến thử nối.
- `catalogue/ParametricTypePlan.csv`: danh mục 131 Type và kích thước catalogue.
- `output/ParametricFlexTests.csv`: kiểm tra từng Type trong Family Editor và sửa thử chiều dài/đường kính.
- `output/ParametricConnectionTests.csv`: kiểm tra chọn đúng Family + Type, kết nối ống, đổi Type trong dự án và kết nối đứng.
- `source/parametric/`: mã nguồn add-in; `source/Parametric*.cs`: mã nguồn bộ dựng và kiểm tra.

Chỉ các dòng PASS trong báo cáo là kết quả đã được xác minh. Không dùng kết quả của bộ thư viện cũ để thay cho báo cáo kiểm tra bộ nhiều Type mới.
