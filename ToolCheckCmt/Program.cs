using System;
using System.Windows.Forms;
using OfficeOpenXml; // Thêm thư viện để nhận diện ExcelPackage

namespace ToolCheckCmt // <--- QUAN TRỌNG: Đổi tên này trùng với tên Project của bạn
{
    static class Program {
        [STAThread]
        static void Main() {
            // Cấu hình License ngay từ đầu để tránh mọi lỗi bản quyền về sau
            // Dùng khối try-catch để tự động nhận diện phiên bản EPPlus (7 hoặc 8 đều chạy được)
            try {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            } catch {
                // Nếu bản mới quá thì dùng lệnh này (dự phòng)
                // ExcelPackage.License.LicenseContext = LicenseContext.NonCommercial;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // SỬA LỖI QUAN TRỌNG: Chỉ gọi new Form1() -> KHÔNG ĐƯỢC CÓ THAM SỐ Ở TRONG NGOẶC
            Application.Run(new Form1());
        }
    }
}