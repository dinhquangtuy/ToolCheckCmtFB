using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D; // Cần thêm cái này để vẽ nút bo tròn
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
        private SemaphoreSlim _semaphore = new SemaphoreSlim(200);

        // --- QUẢN LÝ DỮ LIỆU & UI ---
        private int _countLive = 0;
        private int _countDie = 0;
        private int _totalProcessed = 0;

        private List<string> _listTokens = new List<string>();
        private int _currentTokenIndex = 0;
        private object _tokenLock = new object();

        private ConcurrentQueue<ResultModel> _queueResult = new ConcurrentQueue<ResultModel>();
        private ConcurrentBag<ResultModel> _fullResults = new ConcurrentBag<ResultModel>();
        private System.Windows.Forms.Timer _uiTimer;
        private const string SETTINGS_FILE = "last_session.json";

        // Class phụ
        private class ResultModel {
            public int STT { get; set; }
            public string ID { get; set; }
            public string Status { get; set; }
            public string Type { get; set; }
            public string Date { get; set; }
            public string Link { get; set; }
            public Color Color { get; set; }
        }

        public class AppSettings {
            public string LastTokens { get; set; }
            public string LastLinks { get; set; }
        }

        private static HttpClient CreateHttpClient() {
            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression) {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            var c = new HttpClient(handler);
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return c;
        }

        public Form1() {
            InitializeComponent();

            // --- TỐI ƯU MẠNG ---
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 2000;
            ServicePointManager.Expect100Continue = false;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // --- SETUP CƠ BẢN ---
            SetupDataGridView();
            SetupTimer();

            // --- XỬ LÝ KÉO THẢ ---
            txtLinks.AllowDrop = true;
            txtLinks.DragEnter += txtLinks_DragEnter;
            txtLinks.DragDrop += txtLinks_DragDrop;

            // --- QUAN TRỌNG: LÀM ĐẸP & CO GIÃN CỬA SỔ ---
            ApplyUI_And_Layout();

            this.FormClosing += Form1_FormClosing;
            LoadSettings();
        }

        // ==========================================================
        // KHU VỰC GIAO DIỆN & LAYOUT (MỚI)
        // ==========================================================
        private void ApplyUI_And_Layout() {
            // 1. Cấu hình Form chính
            this.BackColor = Color.FromArgb(245, 247, 251);
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            this.Text = "Tool Check Live/Die - Pro Version";
            this.MinimumSize = new Size(900, 600);

            // 2. Cấu hình DataGridView (Bảng kết quả)
            dgvResult.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            dgvResult.BackgroundColor = Color.White;
            dgvResult.BorderStyle = BorderStyle.None;
            dgvResult.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvResult.EnableHeadersVisualStyles = false;
            dgvResult.GridColor = Color.FromArgb(230, 230, 230);

            // Header Style
            dgvResult.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(59, 130, 246);
            dgvResult.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvResult.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvResult.ColumnHeadersHeight = 45;

            // Row Style
            dgvResult.DefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 242, 255);
            dgvResult.DefaultCellStyle.SelectionForeColor = Color.FromArgb(59, 130, 246);
            dgvResult.RowTemplate.Height = 35;

            // --- 3. CẤU HÌNH Ô NHẬP LINK (QUAN TRỌNG: NGĂN XUỐNG DÒNG) ---
            txtLinks.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            txtLinks.BorderStyle = BorderStyle.FixedSingle;
            txtLinks.BackColor = Color.White;

            // ==> Cấu hình mới: Tắt tự động xuống dòng, hiện thanh cuộn ngang dọc
            txtLinks.WordWrap = false;
            txtLinks.ScrollBars = (RichTextBoxScrollBars)ScrollBars.Both;

            // --- 4. CẤU HÌNH Ô NHẬP TOKEN (QUAN TRỌNG: NGĂN XUỐNG DÒNG) ---
            rtbTokens.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            rtbTokens.BorderStyle = BorderStyle.FixedSingle;
            rtbTokens.BackColor = Color.White;

            // ==> Cấu hình mới: Tắt tự động xuống dòng
            rtbTokens.WordWrap = false;
            rtbTokens.ScrollBars = RichTextBoxScrollBars.Both; // RichTextBox dùng enum khác TextBox xíu

            // 5. Cấu hình Nút bấm
            StyleButton(btnCheck, Color.FromArgb(34, 197, 94)); // Xanh lá
            btnCheck.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            StyleButton(btnExport, Color.FromArgb(59, 130, 246)); // Xanh dương
            btnExport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            // 6. Cấu hình Labels
            lblLive.ForeColor = Color.FromArgb(34, 197, 94);
            lblLive.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblLive.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            lblDie.ForeColor = Color.FromArgb(239, 68, 68);
            lblDie.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblDie.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            lblStatus.ForeColor = Color.Gray;
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        }

        private void StyleButton(Button btn, Color bgColor) {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bgColor;
            btn.ForeColor = Color.White;
            btn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Size = new Size(120, 45); // Kích thước nút chuẩn
        }

        // ==========================================================
        // LOGIC CHÍNH (GIỮ NGUYÊN)
        // ==========================================================

        private void txtLinks_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) {
                    string ext = Path.GetExtension(files[0]).ToLower();
                    if (ext == ".txt" || ext == ".xlsx") {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void txtLinks_DragDrop(object sender, DragEventArgs e) {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string filePath = files[0];
            string ext = Path.GetExtension(filePath).ToLower();
            List<string> loadedLinks = new List<string>();

            try {
                if (ext == ".txt") {
                    loadedLinks = File.ReadAllLines(filePath)
                   .Where(line => !string.IsNullOrWhiteSpace(line))
                   .Select(line => line.Trim())
                   .ToList();
                } else if (ext == ".xlsx") {
                    using (var package = new ExcelPackage(new FileInfo(filePath))) {
                        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                        if (worksheet != null && worksheet.Dimension != null) {
                            int rowCount = worksheet.Dimension.Rows;
                            for (int row = 1; row <= rowCount; row++) {
                                string cellValue = worksheet.Cells[row, 1].Text;
                                if (!string.IsNullOrWhiteSpace(cellValue)) {
                                    loadedLinks.Add(cellValue.Trim());
                                }
                            }
                        }
                    }
                }

                if (loadedLinks.Count > 0) {
                    txtLinks.Text = string.Join(Environment.NewLine, loadedLinks);
                    MessageBox.Show($"Đã nạp {loadedLinks.Count} link!", "Thành công");
                }
            } catch (Exception ex) {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        private void SetupTimer() {
            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 500;
            _uiTimer.Tick += _uiTimer_Tick;
        }

        private void SetupDataGridView() {
            dgvResult.Columns.Clear();
            dgvResult.Columns.Add("colSTT", "STT");
            dgvResult.Columns.Add("colID", "Comment ID");
            dgvResult.Columns.Add("colStatus", "Trạng Thái");
            dgvResult.Columns.Add("colType", "Chi Tiết");
            dgvResult.Columns.Add("colDate", "Ngày");
            dgvResult.Columns.Add("colLink", "Link Gốc");

            dgvResult.Columns["colSTT"].Width = 50;
            dgvResult.Columns["colID"].Width = 120;
            dgvResult.Columns["colStatus"].Width = 100;
            dgvResult.Columns["colType"].Width = 100;
            dgvResult.Columns["colDate"].Width = 120;
            dgvResult.Columns["colLink"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; // Cột Link tự giãn hết cỡ

            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, dgvResult, new object[] { true });
        }

        private void _uiTimer_Tick(object sender, EventArgs e) {
            if (_queueResult.IsEmpty) return;
            List<ResultModel> batch = new List<ResultModel>();
            while (_queueResult.TryDequeue(out var item)) {
                batch.Add(item);
                if (batch.Count >= 100) break;
            }

            if (batch.Count > 0) {
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
                dgvResult.ResumeLayout();

                lblLive.Text = $"Live: {_countLive}";
                lblDie.Text = $"Die: {_countDie}";
                lblStatus.Text = $"Đang chạy: {_totalProcessed}";

                if (dgvResult.RowCount > 0)
                    dgvResult.FirstDisplayedScrollingRowIndex = dgvResult.RowCount - 1;
            }
        }

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

            _countLive = 0; _countDie = 0; _totalProcessed = 0;
            _currentTokenIndex = 0;
            dgvResult.Rows.Clear();
            while (_queueResult.TryDequeue(out _)) { }
            _fullResults = new ConcurrentBag<ResultModel>();

            btnCheck.Enabled = false;
            btnExport.Enabled = false;
            _uiTimer.Start();

            var tasks = new List<Task>();
            int sttCounter = 1;

            foreach (var url in listLinks) {
                int currentSTT = sttCounter++;
                await _semaphore.WaitAsync();

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
            await Task.Delay(1000);
            _uiTimer.Stop();
            _uiTimer_Tick(null, null);

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

            if (string.IsNullOrEmpty(cmtId)) {
                status = "Lỗi ID";
            } else {
                string apiUrl = $"https://graph.facebook.com/v18.0/{cmtId}?fields=id,permalink_url,created_time,is_hidden,object{{created_time,id}},parent{{created_time,id}}&access_token={token}";
                string jsonResponse = await GetApiContent(apiUrl);

                if (jsonResponse.Contains("\"id\":")) {
                    try {
                        JObject json = JObject.Parse(jsonResponse);
                        string realLink = (string)json["permalink_url"] ?? "";
                        bool isHidden = (bool?)json["is_hidden"] ?? false;

                        DateTime? cmtDate = (DateTime?)json["created_time"];
                        DateTime? postDate = null;

                        if (json["object"] != null && json["object"]["created_time"] != null) {
                            postDate = (DateTime?)json["object"]["created_time"];
                        }
                        if (postDate == null && json["parent"] != null && json["parent"]["created_time"] != null) {
                            postDate = (DateTime?)json["parent"]["created_time"];
                        }

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

            var resultItem = new ResultModel {
                STT = stt,
                ID = cmtId,
                Status = status,
                Type = typeResult,
                Date = dateStr,
                Link = url,
                Color = rowColor
            };

            _fullResults.Add(resultItem);
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
            if (_fullResults.IsEmpty) { MessageBox.Show("Không có dữ liệu!"); return; }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = $"ket_qua_comment_{DateTime.Now:HHmm}.xlsx";
            sfd.Filter = "Excel (*.xlsx)|*.xlsx";

            if (sfd.ShowDialog() == DialogResult.OK) {
                try {
                    if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);
                    using (var p = new ExcelPackage(new FileInfo(sfd.FileName))) {
                        var ws = p.Workbook.Worksheets.Add("Data");
                        ws.Cells[1, 1].Value = "Link";

                        int r = 2;
                        var exportList = _fullResults.Where(x => x.Status == "LIVE").OrderBy(x => x.STT).ToList();

                        foreach (var item in exportList) {
                            string link = CleanFacebookLink(item.Link);
                            ws.Cells[r, 1].Value = link;
                            r++;
                        }
                        ws.Column(1).Width = 50;
                        p.Save();
                    }
                    System.Diagnostics.Process.Start(sfd.FileName);
                } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
            }
        }
    }

    // --- CLASS NÚT BẤM BO TRÒN (Tùy chọn dùng nếu muốn thay thế Button thường) ---
    public class RoundedButton : Button {
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            Rectangle Rect = new Rectangle(0, 0, this.Width, this.Height);
            GraphicsPath GraphPath = new GraphicsPath();
            GraphPath.AddArc(Rect.X, Rect.Y, 15, 15, 180, 90);
            GraphPath.AddArc(Rect.X + Rect.Width - 15, Rect.Y, 15, 15, 270, 90);
            GraphPath.AddArc(Rect.X + Rect.Width - 15, Rect.Y + Rect.Height - 15, 15, 15, 0, 90);
            GraphPath.AddArc(Rect.X, Rect.Y + Rect.Height - 15, 15, 15, 90, 90);
            this.Region = new Region(GraphPath);
        }
    }
}