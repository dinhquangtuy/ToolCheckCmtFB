using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        // --- CẤU HÌNH ---
        private static readonly HttpClient client = CreateHttpClient();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(200);

        // --- QUẢN LÝ DỮ LIỆU ---
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

        // Class kết quả
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
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 2000;
            ServicePointManager.Expect100Continue = false;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            SetupDataGridView();
            SetupTimer();

            txtLinks.AllowDrop = true;
            txtLinks.DragEnter += txtLinks_DragEnter;
            txtLinks.DragDrop += txtLinks_DragDrop;

            ApplyUI_And_Layout();
            AutoCreateShortcut();

            this.FormClosing += Form1_FormClosing;
            LoadSettings();
        }

        // ==========================================================
        // UI & UTILS
        // ==========================================================
        private void ApplyUI_And_Layout() {
            this.BackColor = Color.FromArgb(245, 247, 251);
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            this.Text = "Tool Check Live/Die - Pro Version";
            this.MinimumSize = new Size(900, 600);

            dgvResult.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvResult.BackgroundColor = Color.White;
            dgvResult.BorderStyle = BorderStyle.None;
            dgvResult.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvResult.EnableHeadersVisualStyles = false;
            dgvResult.GridColor = Color.FromArgb(230, 230, 230);
            dgvResult.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(59, 130, 246);
            dgvResult.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvResult.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvResult.ColumnHeadersHeight = 45;
            dgvResult.DefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 242, 255);
            dgvResult.DefaultCellStyle.SelectionForeColor = Color.FromArgb(59, 130, 246);
            dgvResult.RowTemplate.Height = 35;

            txtLinks.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            txtLinks.BorderStyle = BorderStyle.FixedSingle;
            txtLinks.BackColor = Color.White;
            txtLinks.WordWrap = false;
            txtLinks.ScrollBars = (RichTextBoxScrollBars)ScrollBars.Both;

            rtbTokens.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            rtbTokens.BorderStyle = BorderStyle.FixedSingle;
            rtbTokens.BackColor = Color.White;
            rtbTokens.WordWrap = false;
            rtbTokens.ScrollBars = RichTextBoxScrollBars.Both;

            StyleButton(btnCheck, Color.FromArgb(34, 197, 94));
            btnCheck.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            StyleButton(btnExport, Color.FromArgb(59, 130, 246));
            btnExport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

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
            btn.Size = new Size(120, 45);
        }

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
                    loadedLinks = File.ReadAllLines(filePath).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToList();
                } else if (ext == ".xlsx") {
                    using (var package = new ExcelPackage(new FileInfo(filePath))) {
                        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                        if (worksheet != null && worksheet.Dimension != null) {
                            int rowCount = worksheet.Dimension.Rows;
                            for (int row = 1; row <= rowCount; row++) {
                                string cellValue = worksheet.Cells[row, 1].Text;
                                if (!string.IsNullOrWhiteSpace(cellValue)) loadedLinks.Add(cellValue.Trim());
                            }
                        }
                    }
                }
                if (loadedLinks.Count > 0) {
                    txtLinks.Text = string.Join(Environment.NewLine, loadedLinks);
                    MessageBox.Show($"Đã nạp {loadedLinks.Count} link!", "Thành công");
                }
            } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
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
            dgvResult.Columns["colType"].Width = 120;
            dgvResult.Columns["colDate"].Width = 120;
            dgvResult.Columns["colLink"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            typeof(DataGridView).InvokeMember("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty, null, dgvResult, new object[] { true });
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
                if (dgvResult.RowCount > 0) dgvResult.FirstDisplayedScrollingRowIndex = dgvResult.RowCount - 1;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) => SaveSettings();

        private void SaveSettings() {
            try {
                var settings = new AppSettings { LastTokens = rtbTokens.Text, LastLinks = txtLinks.Text };
                File.WriteAllText(SETTINGS_FILE, JsonConvert.SerializeObject(settings, Formatting.Indented));
            } catch { }
        }

        private void LoadSettings() {
            try {
                if (File.Exists(SETTINGS_FILE)) {
                    var settings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(SETTINGS_FILE));
                    if (settings != null) { rtbTokens.Text = settings.LastTokens; txtLinks.Text = settings.LastLinks; }
                }
            } catch { }
        }

        // ==========================================================
        // TOKEN UTILS
        // ==========================================================

        private async Task<bool> CheckTokenLive(string token) {
            try {
                string url = $"https://graph.facebook.com/me?fields=id,name&access_token={token}";
                using (var response = await client.GetAsync(url)) {
                    if (!response.IsSuccessStatusCode) return false;
                    string content = await response.Content.ReadAsStringAsync();
                    if (content.Contains("\"error\"") || !content.Contains("\"id\"")) return false;
                    return true;
                }
            } catch { return false; }
        }

        private void HandleTokenDie(string badToken) {
            lock (_tokenLock) {
                if (_listTokens.Contains(badToken)) {
                    _listTokens.Remove(badToken);
                }
            }
        }

        private string GetNextToken() {
            lock (_tokenLock) {
                if (_listTokens.Count == 0) return null;
                if (_currentTokenIndex >= _listTokens.Count) _currentTokenIndex = 0;
                string token = _listTokens[_currentTokenIndex];
                _currentTokenIndex++;
                return token;
            }
        }

        // ==========================================================
        // LOGIC CHẠY CHÍNH
        // ==========================================================

        private async void btnCheck_Click(object sender, EventArgs e) {
            _listTokens = rtbTokens.Lines.Where(x => !string.IsNullOrWhiteSpace(x) && x.Length > 10).Select(x => x.Trim()).ToList();
            var listLinks = txtLinks.Lines.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            if (listLinks.Count == 0) { MessageBox.Show("Chưa nhập Link!"); return; }
            if (_listTokens.Count == 0) { MessageBox.Show("Chưa nhập Token!"); return; }

            btnCheck.Enabled = false;
            btnExport.Enabled = false;

            // --- BƯỚC 1: LỌC TOKEN ĐẦU VÀO ---
            lblStatus.Text = "Đang sàng lọc Token...";
            List<string> liveTokens = new List<string>();
            int checkedCount = 0;

            using (var semToken = new SemaphoreSlim(20)) {
                // SỬA LỖI Ở ĐÂY: Dùng foreach thông thường thay vì lambda expression
                var tasks = new List<Task>();
                foreach (var token in _listTokens) {
                    tasks.Add(Task.Run(async () => {
                        await semToken.WaitAsync();
                        try {
                            bool isLive = await CheckTokenLive(token);
                            if (isLive) {
                                lock (liveTokens) { liveTokens.Add(token); }
                            }

                            Interlocked.Increment(ref checkedCount);
                            Invoke(new Action(() => {
                                lblStatus.Text = $"Check Token: {checkedCount}/{_listTokens.Count} (Sống: {liveTokens.Count})";
                            }));
                        } finally {
                            semToken.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }

            _listTokens = liveTokens;
            rtbTokens.Text = string.Join(Environment.NewLine, _listTokens);

            if (_listTokens.Count == 0) {
                MessageBox.Show("Tất cả Token đều DIE/BLOCK! Không thể chạy.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnCheck.Enabled = true; btnExport.Enabled = true;
                return;
            }

            // --- BƯỚC 2: CHẠY LINK ---
            lblStatus.Text = "Đang check Link...";
            _countLive = 0; _countDie = 0; _totalProcessed = 0;
            _currentTokenIndex = 0;
            dgvResult.Rows.Clear();
            while (_queueResult.TryDequeue(out _)) { }
            _fullResults = new ConcurrentBag<ResultModel>();

            _uiTimer.Start();
            var linkTasks = new List<Task>(); // Đổi tên biến tránh trùng lặp
            int sttCounter = 1;

            foreach (var url in listLinks) {
                int currentSTT = sttCounter++;
                await _semaphore.WaitAsync();

                linkTasks.Add(Task.Run(async () => {
                    try {
                        bool processedSuccess = false;

                        while (!processedSuccess) {
                            string tokenToUse = GetNextToken();

                            if (string.IsNullOrEmpty(tokenToUse)) {
                                var resultItem = new ResultModel {
                                    STT = currentSTT,
                                    Link = url,
                                    Status = "ERROR",
                                    Type = "Hết Token",
                                    Color = Color.Gray
                                };
                                AddToResultQueue(resultItem);
                                break;
                            }

                            processedSuccess = await ProcessLinkWithToken(url.Trim(), tokenToUse, currentSTT);
                        }

                    } finally {
                        _semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(linkTasks);
            await Task.Delay(1000);
            _uiTimer.Stop();
            _uiTimer_Tick(null, null);

            MessageBox.Show($"Hoàn tất!\nLive: {_countLive} - Die: {_countDie}");
            lblStatus.Text = "Hoàn tất.";
            btnCheck.Enabled = true;
            btnExport.Enabled = true;
        }

        private async Task<bool> ProcessLinkWithToken(string url, string token, int stt) {
            string cmtId = ExtractCommentId(url);

            if (string.IsNullOrEmpty(cmtId)) {
                AddToResultQueue(new ResultModel { STT = stt, ID = "", Status = "Lỗi ID", Type = "Sai Format", Date = "N/A", Link = url, Color = Color.Orange });
                return true;
            }

            string apiUrl = $"https://graph.facebook.com/v18.0/{cmtId}?fields=id,permalink_url,created_time,is_hidden,object{{created_time,id}},parent{{created_time,id}}&access_token={token}";

            try {
                using (var response = await client.GetAsync(apiUrl)) {

                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) {
                        HandleTokenDie(token);
                        return false;
                    }

                    if (response.StatusCode == HttpStatusCode.BadRequest) {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        bool isTokenError =
                            errorContent.Contains("OAuth") ||
                            errorContent.Contains("access token") ||
                            errorContent.Contains("\"code\":190") ||
                            errorContent.Contains("checkpoint") ||
                            errorContent.Contains("restricted") ||
                            errorContent.Contains("blocked");

                        if (isTokenError) {
                            HandleTokenDie(token);
                            return false;
                        }

                        AddToResultQueue(new ResultModel { STT = stt, ID = cmtId, Status = "Lỗi Link", Type = "Sai ID/Format", Date = "N/A", Link = url, Color = Color.Orange });
                        return true;
                    }

                    if (response.IsSuccessStatusCode) {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        ResultModel res = new ResultModel { STT = stt, ID = cmtId, Link = url };

                        if (jsonResponse.Contains("\"id\":")) {
                            try {
                                JObject json = JObject.Parse(jsonResponse);
                                string realLink = (string)json["permalink_url"] ?? "";
                                bool isHidden = (bool?)json["is_hidden"] ?? false;
                                DateTime? cmtDate = (DateTime?)json["created_time"];
                                DateTime? postDate = null;

                                if (json["object"] != null && json["object"]["created_time"] != null) postDate = (DateTime?)json["object"]["created_time"];
                                if (postDate == null && json["parent"] != null && json["parent"]["created_time"] != null) postDate = (DateTime?)json["parent"]["created_time"];

                                if (postDate == null) {
                                    string postId = "";
                                    if (json["object"] != null && json["object"]["id"] != null) postId = json["object"]["id"].ToString();
                                    if (string.IsNullOrEmpty(postId) && !string.IsNullOrEmpty(realLink)) postId = ExtractPostIdFromLink(realLink);
                                    if (!string.IsNullOrEmpty(postId)) {
                                        try {
                                            string postApi = $"https://graph.facebook.com/v18.0/{postId}?fields=created_time&access_token={token}";
                                            string pJsonStr = await (await client.GetAsync(postApi)).Content.ReadAsStringAsync();
                                            JObject pJson = JObject.Parse(pJsonStr);
                                            if (pJson["created_time"] != null) postDate = (DateTime?)pJson["created_time"];
                                        } catch { }
                                    }
                                }

                                DateTime? targetDate = postDate ?? cmtDate;
                                res.Date = targetDate.HasValue ? targetDate.Value.ToString("dd/MM/yyyy") : "N/A";

                                if (isHidden) {
                                    res.Status = "DIE"; res.Type = "Bị Ẩn"; res.Color = Color.Salmon;
                                    Interlocked.Increment(ref _countDie);
                                } else {
                                    res.Status = "LIVE"; res.Type = postDate != null ? "OK (Post)" : "OK (Cmt)"; res.Color = Color.LightGreen;
                                    Interlocked.Increment(ref _countLive);
                                }
                            } catch {
                                res.Status = "Lỗi JSON"; res.Type = "Parse Error"; res.Color = Color.Salmon;
                            }
                        } else {
                            res.Status = "DIE"; res.Type = "Content Rỗng"; res.Color = Color.Salmon;
                            Interlocked.Increment(ref _countDie);
                        }

                        AddToResultQueue(res);
                        return true;
                    }

                    AddToResultQueue(new ResultModel { STT = stt, ID = cmtId, Status = "DIE", Type = $"HTTP {response.StatusCode}", Date = "N/A", Link = url, Color = Color.Salmon });
                    Interlocked.Increment(ref _countDie);
                    return true;
                }
            } catch {
                return false;
            }
        }

        private void AddToResultQueue(ResultModel item) {
            Interlocked.Increment(ref _totalProcessed);
            _fullResults.Add(item);
            _queueResult.Enqueue(item);
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
                var matchPfbid = Regex.Match(url, @"(pfbid[a-zA-Z0-9]+)");
                if (matchPfbid.Success) return matchPfbid.Groups[1].Value;
                var matchFbid = Regex.Match(url, @"story_fbid=([0-9]+)");
                if (matchFbid.Success) return matchFbid.Groups[1].Value;
                var matchPost = Regex.Match(url, @"\/posts\/([a-zA-Z0-9]+)");
                if (matchPost.Success) return matchPost.Groups[1].Value;
                var matchVideo = Regex.Match(url, @"\/videos\/([0-9]+)");
                if (matchVideo.Success) return matchVideo.Groups[1].Value;
                var matchPhoto = Regex.Match(url, @"\/photos\/[a-zA-Z0-9\.]+\/([0-9]+)");
                if (matchPhoto.Success) return matchPhoto.Groups[1].Value;
                var matchEnd = Regex.Match(url, @"\/([0-9]+)\/?(?:\?|$)");
                if (matchEnd.Success && matchEnd.Groups[1].Value.Length > 8) return matchEnd.Groups[1].Value;
            } catch { }
            return "";
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
                            ws.Cells[r, 1].Value = item.Link;
                            r++;
                        }
                        ws.Column(1).Width = 50;
                        p.Save();
                    }
                    System.Diagnostics.Process.Start(sfd.FileName);
                } catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
            }
        }

        private void AutoCreateShortcut() {
            try {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutName = "Tool Check CMT.lnk";
                string shortcutPath = Path.Combine(desktopPath, shortcutName);
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                if (!System.IO.File.Exists(shortcutPath)) {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = exePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                    shortcut.WindowStyle = 1;
                    shortcut.Description = "Tool Check Live/Die Facebook";
                    shortcut.IconLocation = exePath + ",0";
                    shortcut.Save();
                }
            } catch { }
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