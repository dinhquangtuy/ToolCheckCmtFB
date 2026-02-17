using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ToolCheckCmt {
    public class FacebookApiService {
        private static readonly HttpClient _client = CreateHttpClient();

        private static HttpClient CreateHttpClient() {
            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression) {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            var c = new HttpClient(handler);
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return c;
        }

        public async Task<string> GetApiContentAsync(string apiUrl) {
            try {
                using (var response = await _client.GetAsync(apiUrl)) {
                    return await response.Content.ReadAsStringAsync();
                }
            } catch { return ""; }
        }

        public bool IsTokenError(string jsonResponse) {
            if (string.IsNullOrEmpty(jsonResponse)) return false;
            if (!jsonResponse.Contains("\"error\"")) return false;

            try {
                JObject json = JObject.Parse(jsonResponse);
                if (json["error"] != null) {
                    int code = (int?)json["error"]["code"] ?? 0;
                    if (code == 190 || code == 4 || code == 17 || code == 32 || code == 613) {
                        return true;
                    }
                }
            } catch { }
            return false;
        }

        public async Task<ResultModel> ProcessSingleLinkAsync(string url, int stt, TokenManager tokenManager, ConcurrentDictionary<string, string> usernameCache) {
            string cmtId = FacebookParser.ExtractCommentId(url);
            string status = "ERROR";
            string typeResult = "";
            string dateStr = "N/A";
            Color rowColor = Color.White;
            string finalLink = url;

            if (string.IsNullOrEmpty(cmtId)) {
                return CreateResult(stt, cmtId, "Lỗi ID", "", dateStr, finalLink, Color.White);
            }

            bool isSuccess = false;
            HashSet<string> triedTokens = new HashSet<string>(); // Sổ tay nhớ các Token đã thử

            while (!isSuccess) {
                string currentToken = tokenManager.GetNextToken();
                if (string.IsNullOrEmpty(currentToken)) {
                    return CreateResult(stt, cmtId, "HẾT TOKEN", "All Tokens Dead", dateStr, finalLink, Color.Red);
                }

                // Chống lặp vô hạn: Nếu xoay 1 vòng quay lại trúng token cũ -> Chốt Die
                if (triedTokens.Contains(currentToken)) {
                    status = "DIE"; typeResult = "Die Thực/Xóa/Chặn"; rowColor = Color.Salmon;
                    break;
                }
                triedTokens.Add(currentToken);

                string apiUrl = $"https://graph.facebook.com/v18.0/{cmtId}?fields=id,permalink_url,created_time,is_hidden,object{{created_time,id}},parent{{created_time,id}}&access_token={currentToken}";
                string jsonResponse = await GetApiContentAsync(apiUrl);

                if (IsTokenError(jsonResponse)) {
                    tokenManager.RemoveDeadToken(currentToken);
                    continue; // Lỗi Token thì vòng lại lấy token khác
                }

                if (jsonResponse.Contains("\"id\":")) {
                    try {
                        JObject json = JObject.Parse(jsonResponse);
                        string realLink = (string)json["permalink_url"] ?? "";
                        finalLink = realLink;

                        bool isHidden = (bool?)json["is_hidden"] ?? false;
                        DateTime? cmtDate = (DateTime?)json["created_time"];
                        DateTime? postDate = null;

                        if (json["object"] != null && json["object"]["created_time"] != null) postDate = (DateTime?)json["object"]["created_time"];
                        if (postDate == null && json["parent"] != null && json["parent"]["created_time"] != null) postDate = (DateTime?)json["parent"]["created_time"];

                        if (postDate == null) {
                            string postId = json["object"]?["id"]?.ToString() ?? "";
                            if (string.IsNullOrEmpty(postId) && !string.IsNullOrEmpty(realLink)) postId = FacebookParser.ExtractPostIdFromLink(realLink);

                            if (!string.IsNullOrEmpty(postId)) {
                                string postApiUrl = $"https://graph.facebook.com/v18.0/{postId}?fields=created_time&access_token={currentToken}";
                                string postJson = await GetApiContentAsync(postApiUrl);

                                if (IsTokenError(postJson)) {
                                    tokenManager.RemoveDeadToken(currentToken);
                                    continue;
                                }
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
                        } else if (realLink.Contains("/reel/")) {
                            status = "DIE"; typeResult = "Reel"; rowColor = Color.Salmon;
                        } else if (daysDiff > 27) {
                            status = "DIE"; typeResult = "Bài Cũ"; rowColor = Color.Salmon;
                        } else {
                            status = "LIVE";
                            typeResult = postDate != null ? "OK (Post)" : "OK (Cmt)";
                            rowColor = Color.LightGreen;

                            string pageId = FacebookParser.ExtractPageIdFromLink(realLink);

                            if (!realLink.Contains("comment_id=") && !string.IsNullOrEmpty(cmtId)) {
                                realLink += (realLink.Contains("?") ? "&" : "?") + "comment_id=" + cmtId;
                            }

                            // --- BỘ MÁY XỬ LÝ USERNAME THÔNG MINH (CHỐNG N+1 QUERY BẢO VỆ TOKEN) ---
                            if (!string.IsNullOrEmpty(pageId)) {
                                string username = "";

                                // 1. Vớt Username thẳng từ URL gốc (nếu có sẵn)
                                var matchUser = Regex.Match(realLink, @"facebook\.com\/([^\/\?]+)");
                                if (matchUser.Success && matchUser.Groups[1].Value != "permalink.php" && !Regex.IsMatch(matchUser.Groups[1].Value, @"^\d+$")) {
                                    username = matchUser.Groups[1].Value;
                                    usernameCache.TryAdd(pageId, username); // Ghi ngay vào sổ
                                }

                                // 2. Nếu URL không có, lật sổ tay ra tra cứu
                                if (string.IsNullOrEmpty(username)) {
                                    if (usernameCache.TryGetValue(pageId, out string cachedName)) {
                                        username = cachedName; // Có trong sổ -> Xài luôn, KHÔNG GỌI API
                                    } else {
                                        // 3. Sổ trắng -> Bắt buộc hy sinh 1 lần gọi API
                                        string userApiUrl = $"https://graph.facebook.com/v18.0/{pageId}?fields=username&access_token={currentToken}";
                                        string userJson = await GetApiContentAsync(userApiUrl);

                                        if (IsTokenError(userJson)) {
                                            tokenManager.RemoveDeadToken(currentToken);
                                            continue;
                                        }

                                        if (!string.IsNullOrEmpty(userJson)) {
                                            try {
                                                JObject uJson = JObject.Parse(userJson);
                                                if (uJson["username"] != null) {
                                                    username = uJson["username"].ToString();
                                                }
                                            } catch { }
                                        }

                                        // 4. Bịt lỗ hổng: Ghi vào sổ DÙ CÓ HAY KHÔNG CÓ USERNAME để lần sau không phải hỏi lại
                                        if (string.IsNullOrEmpty(username)) {
                                            usernameCache.TryAdd(pageId, "NO_USER");
                                            username = "NO_USER";
                                        } else {
                                            usernameCache.TryAdd(pageId, username);
                                        }
                                    }
                                }

                                // 5. Lắp ráp link: Dùng hàm ConvertToCleanPermalink
                                if (username == "NO_USER" || string.IsNullOrEmpty(username)) {
                                    finalLink = FacebookParser.ConvertToCleanPermalink(realLink); // Ép ra Permalink số SẠCH
                                } else {
                                    finalLink = FacebookParser.ConvertToCleanPermalink(realLink, username); // Ép ra Username SẠCH
                                    typeResult += " + User";
                                }
                            } else {
                                finalLink = realLink; // Không tìm được Page ID thì đành giữ nguyên
                            }
                        }
                        isSuccess = true;
                    } catch {
                        status = "Lỗi JSON"; isSuccess = true;
                    }
                } else {
                    // Mạng ok, Token ok, nhưng API không nôn ra ID -> Chốt Die bài viết
                    status = "DIE"; typeResult = "Die Thực/Xóa"; rowColor = Color.Salmon;
                    isSuccess = true;
                }
            }

            return CreateResult(stt, cmtId, status, typeResult, dateStr, finalLink, rowColor);
        }

        private ResultModel CreateResult(int stt, string id, string status, string type, string date, string link, Color color) {
            return new ResultModel { STT = stt, ID = id, Status = status, Type = type, Date = date, Link = link, Color = color };
        }
    }
}