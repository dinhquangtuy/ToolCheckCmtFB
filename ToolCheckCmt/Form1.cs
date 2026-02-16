using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ToolCheckCmt {
    public partial class Form1 : Form {
        // --- CÁC CLASS CỘNG TÁC ---
        private readonly TokenManager _tokenManager = new TokenManager();
        private readonly FacebookApiService _apiService = new FacebookApiService();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(25);

        // --- QUẢN LÝ DỮ LIỆU & UI ---
        private int _countLive = 0;
        private int _countDie = 0;
        private int _totalProcessed = 0;

        private ConcurrentQueue<ResultModel> _queueResult = new ConcurrentQueue<ResultModel>();
        private ConcurrentBag<ResultModel> _fullResults = new ConcurrentBag<ResultModel>();
        private ConcurrentDictionary<string, string> _usernameCache = new ConcurrentDictionary<string, string>();

        private System.Windows.Forms.Timer _uiTimer;

        public Form1() {
            InitializeComponent();
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            System.Net.ServicePointManager.DefaultConnectionLimit = 2000;

            SetupDataGridView();
            SetupTimer();
            ApplyUI_And_Layout();

            AppConfigManager.AutoCreateShortcut();

            txtLinks.AllowDrop = true;
            txtLinks.DragEnter += txtLinks_DragEnter;
            txtLinks.DragDrop += txtLinks_DragDrop;

            this.FormClosing += Form1_FormClosing;

            var settings = AppConfigManager.LoadSettings();
            if (settings != null) {
                rtbTokens.Text = settings.LastTokens;
                txtLinks.Text = settings.LastLinks;
            }
        }

        private void ApplyUI_And_Layout() {
            this.BackColor = Color.FromArgb(245, 247, 251);
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            this.Text = "Phần mềm RefineMeta";
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

        private void SetupDataGridView() {
            dgvResult.Columns.Clear();
            dgvResult.Columns.Add("colSTT", "STT");
            dgvResult.Columns.Add("colID", "Comment ID");
            dgvResult.Columns.Add("colStatus", "Trạng Thái");
            dgvResult.Columns.Add("colType", "Chi Tiết");
            dgvResult.Columns.Add("colDate", "Ngày");
            dgvResult.Columns.Add("colLink", "Link Cuối Cùng");

            dgvResult.Columns["colSTT"].Width = 50;
            dgvResult.Columns["colID"].Width = 120;
            dgvResult.Columns["colStatus"].Width = 100;
            dgvResult.Columns["colType"].Width = 100;
            dgvResult.Columns["colDate"].Width = 120;
            dgvResult.Columns["colLink"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, dgvResult, new object[] { true });
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
                    loadedLinks = File.ReadAllLines(filePath)
                                      .Where(line => !string.IsNullOrWhiteSpace(line))
                                      .Select(line => line.Trim())
                                      .ToList();
                } else if (ext == ".xlsx") {
                    loadedLinks = ExcelHelper.ReadLinksFromExcel(filePath);
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
            _uiTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _uiTimer.Tick += _uiTimer_Tick;
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

        private async void btnCheck_Click(object sender, EventArgs e) {
            var lines = rtbTokens.Lines.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim());
            _tokenManager.LoadTokens(lines);

            var listLinks = txtLinks.Lines.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            if (listLinks.Count == 0) { MessageBox.Show("Chưa nhập Link!"); return; }
            if (_tokenManager.GetAliveCount() == 0) { MessageBox.Show("Chưa nhập Token!"); return; }

            _countLive = 0; _countDie = 0; _totalProcessed = 0;
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
                        var result = await _apiService.ProcessSingleLinkAsync(url, currentSTT, _tokenManager, _usernameCache);

                        Interlocked.Increment(ref _totalProcessed);
                        if (result.Status == "LIVE") Interlocked.Increment(ref _countLive);
                        else if (result.Status == "DIE" || result.Status.Contains("Lỗi")) Interlocked.Increment(ref _countDie);

                        _fullResults.Add(result);
                        _queueResult.Enqueue(result);
                    } finally {
                        _semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(1000);
            _uiTimer.Stop();
            _uiTimer_Tick(null, null);

            rtbTokens.Text = string.Join(Environment.NewLine, _tokenManager.GetAllAliveTokens());
            MessageBox.Show($"Hoàn tất!\nLive: {_countLive} - Die: {_countDie}\nCòn lại {_tokenManager.GetAliveCount()} Token sống.", "Thông báo");

            btnCheck.Enabled = true;
            btnExport.Enabled = true;
        }

        private void btnExport_Click(object sender, EventArgs e) {
            if (_fullResults.IsEmpty) { MessageBox.Show("Không có dữ liệu!"); return; }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = $"ket_qua_comment.xlsx";
            sfd.Filter = "Excel (*.xlsx)|*.xlsx";

            if (sfd.ShowDialog() == DialogResult.OK) {
                try {
                    Random rnd = new Random();

                    // 1. Nhóm toàn bộ kết quả LIVE theo Page (Giữ nguyên cục, KHÔNG băm nhỏ)
                    var groupedData = _fullResults
                        .Where(x => x.Status == "LIVE")
                        .GroupBy(x => FacebookParser.GetSortingKey(x.Link))
                        .ToList();

                    // 2. Phân tách và TRÁO ĐỔI NGẪU NHIÊN thứ tự các Fanpage 
                    // (Đánh lừa thị giác để không bị lộ quy luật xếp A-Z)
                    var userGroups = groupedData
                        .Where(g => !Regex.IsMatch(g.Key, @"^\d+$"))
                        .OrderBy(g => Guid.NewGuid())
                        .ToList();

                    var idGroups = groupedData
                        .Where(g => Regex.IsMatch(g.Key, @"^\d+$"))
                        .OrderBy(g => Guid.NewGuid())
                        .ToList();

                    // 3. Thuật toán trộn cụm ngẫu nhiên
                    List<ResultModel> exportList = new List<ResultModel>();
                    int userGrpIdx = 0;
                    int idGrpIdx = 0;

                    while (userGrpIdx < userGroups.Count || idGrpIdx < idGroups.Count) {

                        // Đổ xúc xắc: Lần này bốc mấy Page Username? (Ngẫu nhiên từ 2 đến 4 Page)
                        int randomUserCount = rnd.Next(2, 5);
                        for (int i = 0; i < randomUserCount && userGrpIdx < userGroups.Count; i++) {
                            // Nhặt TOÀN BỘ comment của Page này ném vào danh sách
                            exportList.AddRange(userGroups[userGrpIdx++].OrderBy(x => x.STT));
                        }

                        // Đổ xúc xắc: Lần này bốc mấy Page ID số? (Ngẫu nhiên từ 1 đến 2 Page)
                        int randomIdCount = rnd.Next(1, 3);
                        for (int i = 0; i < randomIdCount && idGrpIdx < idGroups.Count; i++) {
                            exportList.AddRange(idGroups[idGrpIdx++].OrderBy(x => x.STT));
                        }
                    }

                    // 4. Xuất file
                    ExcelHelper.ExportToExcel(exportList, sfd.FileName);
                    System.Diagnostics.Process.Start(sfd.FileName);
                } catch (Exception ex) {
                    MessageBox.Show("Lỗi xuất file: " + ex.Message);
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            AppConfigManager.SaveSettings(rtbTokens.Text, txtLinks.Text);
        }
    }
}