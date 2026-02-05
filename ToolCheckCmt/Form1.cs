using Newtonsoft.Json; // Cần NuGet: Newtonsoft.Json
using Newtonsoft.Json.Linq;
using OfficeOpenXml; // Cần NuGet: EPPlus
using System;
using System.Collections.Concurrent; // MỚI: Dùng cho hàng đợi đa luồng
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ToolCheckCmt {
    public partial class Form1 : Form {
        // --- CẤU HÌNH TỐI ƯU ---
        private static readonly HttpClient client = CreateHttpClient();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(200); // Tăng lên 200 luồng

        // --- QUẢN LÝ DỮ LIỆU & UI ---
        private int _countLive = 0;
        private int _countDie = 0;
        private int _totalProcessed = 0;

        private List<string> _listTokens = new List<string>();
        private int _currentTokenIndex = 0;
        private object _tokenLock = new object();

        // Queue và Timer để update UI mượt mà (Không dùng Invoke liên tục)
        private ConcurrentQueue<ResultModel> _queueResult = new ConcurrentQueue<ResultModel>();
        private System.Windows.Forms.Timer _uiTimer;

        // File lưu cài đặt
        private const string SETTINGS_FILE = "last_session.json";

        // Class phụ để lưu dữ liệu vào hàng đợi
        private class ResultModel {
            public int STT { get; set; }
            public string ID { get; set; }
            public string Status { get; set; }
            public string Type { get; set; }
            public string Date { get; set; }
            public string Link { get; set; }
            public Color Color { get; set; }
        }

        // Class để lưu cài đặt (Token, Link cũ)
        public class AppSettings {
            public string LastTokens { get; set; }
            public string LastLinks { get; set; }
        }

        private static HttpClient CreateHttpClient() {
            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression) {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            // Tắt xác thực SSL để chạy nhanh hơn và tránh lỗi SSL cũ
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            var c = new HttpClient(handler);
            c.Timeout = TimeSpan.FromSeconds(15); // Giảm timeout xuống để skip nhanh lỗi
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return c;
        }

        public Form1() {
            InitializeComponent();

            // --- TỐI ƯU KẾT NỐI MẠNG ---
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 2000; // Cho phép mở 2000 kết nối đồng thời
            ServicePointManager.Expect100Continue = false;

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            SetupDataGridView();
            SetupTimer();

            // Đăng ký sự kiện đóng form để lưu dữ liệu
            this.FormClosing += Form1_FormClosing;

            // Tải dữ liệu cũ khi mở tool
            LoadSettings();
        }

        private void SetupTimer() {
            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 500; // Cập nhật giao diện mỗi 0.5s
            _uiTimer.Tick += _uiTimer_Tick;
        }

        private void SetupDataGridView() {
            dgvResult.Columns.Clear();
            dgvResult.Columns.Add("colSTT", "STT");
            dgvResult.Columns.Add("colID", "Comment ID");
            dgvResult.Columns.Add("colStatus", "Trạng Thái");
            dgvResult.Columns.Add("colType", "Chi Tiết");
            dgvResult.Columns.Add("colDate", "Ngày Bài Gốc");
            dgvResult.Columns.Add("colLink", "Link Gốc");

            dgvResult.Columns["colSTT"].Width = 40;
            dgvResult.Columns["colID"].Width = 110;
            dgvResult.Columns["colStatus"].Width = 80;
            dgvResult.Columns["colType"].Width = 100;
            dgvResult.Columns["colDate"].Width = 100;
            dgvResult.Columns["colLink"].Width = 300;

            // Bật double buffer cho DGV đỡ nháy
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, dgvResult, new object[] { true });
        }

        // --- SỰ KIỆN TIMER: CẬP NHẬT UI TỪ QUEUE ---
        private void _uiTimer_Tick(object sender, EventArgs e) {
            if (_queueResult.IsEmpty) return;

            List<ResultModel> batch = new List<ResultModel>();
            // Lấy tối đa 50 item mỗi lần tick để UI không bị đơ
            while (_queueResult.TryDequeue(out var item)) {
                batch.Add(item);
                if (batch.Count >= 100) break;
            }

            if (batch.Count > 0) {
                // Tắt vẽ tạm thời
                dgvResult.SuspendLayout();
                foreach (var item in batch) {
                    int idx = dgvResult.Rows.Add();
                    var row = dgvResult.Rows[idx];
                    row.Cells["colSTT"].Value = item.STT;
                    row.Cells["colID"].Value = item.ID;
                    row.Cells["colStatus"].Value = item.Status;
                    row.Cells["colType"].Value = item.Type;
                    row.Cells["colDate"].Value = item.Date;
                    row.Cells["colLink"].Value = item.Link;
                    row.DefaultCellStyle.BackColor = item.Color;
                }
                dgvResult.ResumeLayout(); // Bật lại vẽ

                // Update Labels
                lblLive.Text = $"Live: {_countLive}";
                lblDie.Text = $"Die: {_countDie}";
                lblStatus.Text = $"Đang chạy: {_totalProcessed}";

                // Auto Scroll xuống dưới (nếu muốn nhanh hơn thì bỏ dòng này)
                if (dgvResult.RowCount > 0)
                    dgvResult.FirstDisplayedScrollingRowIndex = dgvResult.RowCount - 1;
            }
        }

        // --- LƯU VÀ TẢI CÀI ĐẶT ---
        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            SaveSettings();
        }

        private void SaveSettings() {
            try {
                var settings = new AppSettings {
                    LastTokens = rtbTokens.Text,
                    LastLinks = txtLinks.Text
                };
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SETTINGS_FILE, json);
            } catch { }
        }

        private void LoadSettings() {
            try {
                if (File.Exists(SETTINGS_FILE)) {
                    string json = File.ReadAllText(SETTINGS_FILE);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null) {
                        rtbTokens.Text = settings.LastTokens;
                        txtLinks.Text = settings.LastLinks;
                    }
                }
            } catch { }
        }

        private string GetNextToken() {
            lock (_tokenLock) {
                if (_listTokens.Count == 0) return "";
                string token = _listTokens[_currentTokenIndex];
                _currentTokenIndex++;
                if (_currentTokenIndex >= _listTokens.Count) _currentTokenIndex = 0;
                return token;
            }
        }

        private async void btnCheck_Click(object sender, EventArgs e) {
            _listTokens = rtbTokens.Lines
                .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length > 10)
                .Select(x => x.Trim())
                .ToList();

            var listLinks = txtLinks.Lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            if (listLinks.Count == 0) { MessageBox.Show("Chưa nhập Link!"); return; }
            if (_listTokens.Count == 0) { MessageBox.Show("Chưa nhập Token!"); return; }

            // Reset biến
            _countLive = 0; _countDie = 0; _totalProcessed = 0;
            _currentTokenIndex = 0;
            dgvResult.Rows.Clear();

            // Xóa sạch hàng đợi cũ
            while (_queueResult.TryDequeue(out _)) { }

            btnCheck.Enabled = false;
            btnExport.Enabled = false;

            // Bắt đầu Timer cập nhật UI
            _uiTimer.Start();

            var tasks = new List<Task>();
            int sttCounter = 1;

            foreach (var url in listLinks) {
                int currentSTT = sttCounter++;
                await _semaphore.WaitAsync(); // Kiểm soát số luồng

                tasks.Add(Task.Run(async () => {
                    try {
                        string tokenToUse = GetNextToken();
                        await ProcessLinkWithToken(url.Trim(), tokenToUse, currentSTT);
                    } finally {
                        _semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Đợi Timer quét nốt những item cuối cùng
            await Task.Delay(1000);
            _uiTimer.Stop();
            _uiTimer_Tick(null, null); // Quét lần cuối

            MessageBox.Show($"Hoàn tất!\nLive: {_countLive} - Die: {_countDie}");
            btnCheck.Enabled = true;
            btnExport.Enabled = true;
        }

        private async Task ProcessLinkWithToken(string url, string token, int stt) {
            string cmtId = ExtractCommentId(url);
            string status = "ERROR";
            string typeResult = "";
            string dateStr = "N/A";
            Color rowColor = Color.White;
            bool isSuccess = false;

            if (string.IsNullOrEmpty(cmtId)) {
                status = "Lỗi ID";
            } else {
                // Gọi API lấy thông tin (Thêm parent, from để lấy nhiều info hơn 1 lần)
                string apiUrl = $"https://graph.facebook.com/v18.0/{cmtId}?fields=id,permalink_url,created_time,is_hidden,object{{created_time,id}},parent{{created_time,id}}&access_token={token}";
                string jsonResponse = await GetApiContent(apiUrl);

                if (jsonResponse.Contains("\"id\":")) {
                    isSuccess = true;
                    try {
                        JObject json = JObject.Parse(jsonResponse);
                        string realLink = (string)json["permalink_url"] ?? "";
                        bool isHidden = (bool?)json["is_hidden"] ?? false;

                        DateTime? cmtDate = (DateTime?)json["created_time"];
                        DateTime? postDate = null;

                        // 1. Lấy ngày Post từ object (nếu có)
                        if (json["object"] != null && json["object"]["created_time"] != null) {
                            postDate = (DateTime?)json["object"]["created_time"];
                        }
                        // 2. Nếu không có, lấy ngày Post từ Parent (Nếu comment nằm trong comment khác hoặc post)
                        if (postDate == null && json["parent"] != null && json["parent"]["created_time"] != null) {
                            postDate = (DateTime?)json["parent"]["created_time"];
                        }

                        // 3. Nếu vẫn không có, mới dùng logic tách ID gọi lại (Hạn chế gọi cái này để nhanh hơn)
                        if (postDate == null) {
                            string postId = "";
                            if (json["object"] != null && json["object"]["id"] != null) postId = json["object"]["id"].ToString();
                            if (string.IsNullOrEmpty(postId) && !string.IsNullOrEmpty(realLink)) postId = ExtractPostIdFromLink(realLink);

                            if (!string.IsNullOrEmpty(postId)) {
                                string postApiUrl = $"https://graph.facebook.com/v18.0/{postId}?fields=created_time&access_token={token}";
                                string postJson = await GetApiContent(postApiUrl);
                                try {
                                    JObject pJson = JObject.Parse(postJson);
                                    if (pJson["created_time"] != null) postDate = (DateTime?)pJson["created_time"];
                                } catch { }
                            }
                        }

                        DateTime? targetDate = postDate ?? cmtDate;
                        dateStr = targetDate.HasValue ? targetDate.Value.ToString("dd/MM/yyyy") : "N/A";
                        double daysDiff = targetDate.HasValue ? (DateTime.Now - targetDate.Value).TotalDays : 9999;

                        if (isHidden) {
                            status = "DIE"; typeResult = "Bị Ẩn"; rowColor = Color.Salmon;
                            Interlocked.Increment(ref _countDie);
                        } else if (realLink.Contains("/reel/")) {
                            status = "DIE"; typeResult = "Reel"; rowColor = Color.Salmon;
                            Interlocked.Increment(ref _countDie);
                        } else if (daysDiff > 30) {
                            status = "DIE"; typeResult = "Bài Cũ"; rowColor = Color.Salmon;
                            Interlocked.Increment(ref _countDie);
                        } else {
                            status = "LIVE";
                            typeResult = postDate != null ? "OK (Post)" : "OK (Cmt)";
                            rowColor = Color.LightGreen;
                            Interlocked.Increment(ref _countLive);
                        }

                    } catch { status = "Lỗi JSON"; }
                } else {
                    status = "DIE"; typeResult = "Die Token/Xóa"; rowColor = Color.Salmon;
                    Interlocked.Increment(ref _countDie);
                }
            }

            Interlocked.Increment(ref _totalProcessed);

            // --- THAY VÌ INVOKE, TA ĐẨY VÀO QUEUE ---
            var resultItem = new ResultModel {
                STT = stt,
                ID = cmtId,
                Status = status,
                Type = typeResult,
                Date = dateStr,
                Link = url,
                Color = rowColor
            };
            _queueResult.Enqueue(resultItem);
        }

        private async Task<string> GetApiContent(string apiUrl) {
            try {
                using (var response = await client.GetAsync(apiUrl)) {
                    return await response.Content.ReadAsStringAsync();
                }
            } catch { return ""; }
        }

        private string ExtractCommentId(string url) {
            var match = Regex.Match(url, @"comment_id=(\d+)");
            if (match.Success) return match.Groups[1].Value;
            var match2 = Regex.Match(url, @"reply_comment_id=(\d+)");
            if (match2.Success) return match2.Groups[1].Value;
            if (Regex.IsMatch(url, @"^\d+$")) return url;
            return "";
        }

        private string ExtractPostIdFromLink(string url) {
            try {
                var matchFbid = Regex.Match(url, @"story_fbid=([0-9]+)");
                if (matchFbid.Success) return matchFbid.Groups[1].Value;
                var matchPost = Regex.Match(url, @"\/posts\/([0-9]+)");
                if (matchPost.Success) return matchPost.Groups[1].Value;
                var matchVideo = Regex.Match(url, @"\/videos\/([0-9]+)");
                if (matchVideo.Success) return matchVideo.Groups[1].Value;
                var matchPhoto = Regex.Match(url, @"\/photos\/[a-zA-Z0-9\.]+\/([0-9]+)");
                if (matchPhoto.Success) return matchPhoto.Groups[1].Value;
                var matchEnd = Regex.Match(url, @"\/([0-9]+)\/?(?:\?|$)");
                if (matchEnd.Success && matchEnd.Groups[1].Value.Length > 10) return matchEnd.Groups[1].Value;
            } catch { }
            return "";
        }

        private string CleanFacebookLink(string originalUrl) {
            try {
                if (originalUrl.Contains("story.php")) {
                    var matchId = Regex.Match(originalUrl, @"[?&]id=(\d+)");
                    var matchPostId = Regex.Match(originalUrl, @"[?&]story_fbid=([^&]+)");
                    var matchCmtId = Regex.Match(originalUrl, @"[?&]comment_id=(\d+)");
                    if (matchId.Success && matchPostId.Success && matchCmtId.Success) {
                        return $"https://www.facebook.com/{matchId.Groups[1].Value}/posts/{matchPostId.Groups[1].Value}?comment_id={matchCmtId.Groups[1].Value}";
                    }
                }
                return originalUrl;
            } catch { return originalUrl; }
        }

        private void btnExport_Click(object sender, EventArgs e) {
            if (_countLive == 0) { MessageBox.Show("Không có link LIVE hợp lệ!"); return; }
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = $"List_Links_{DateTime.Now:HHmm}.xlsx";
            sfd.Filter = "Excel (*.xlsx)|*.xlsx";

            if (sfd.ShowDialog() == DialogResult.OK) {
                try {
                    if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);
                    using (var p = new ExcelPackage(new FileInfo(sfd.FileName))) {
                        var ws = p.Workbook.Worksheets.Add("Data");
                        ws.Cells[1, 1].Value = "Danh sách link";
                        int r = 2;
                        foreach (DataGridViewRow row in dgvResult.Rows) {
                            if (row.Cells["colStatus"].Value?.ToString() == "LIVE") {
                                string link = CleanFacebookLink(row.Cells["colLink"].Value?.ToString());
                                ws.Cells[r, 1].Value = link;
                                r++;
                            }
                        }
                        ws.Column(1).Width = 50;
                        p.Save();
                    }
                    System.Diagnostics.Process.Start(sfd.FileName);
                } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
            }
        }
    }
}