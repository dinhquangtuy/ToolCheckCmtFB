using System.Text.RegularExpressions;

namespace ToolCheckCmt {
    public static class FacebookParser {
        public static string ExtractPageIdFromLink(string url) {
            var match = Regex.Match(url, @"facebook\.com\/(\d+)");
            if (match.Success) return match.Groups[1].Value;
            return null;
        }

        public static string ExtractCommentId(string url) {
            var match = Regex.Match(url, @"comment_id=(\d+)");
            if (match.Success) return match.Groups[1].Value;

            var match2 = Regex.Match(url, @"reply_comment_id=(\d+)");
            if (match2.Success) return match2.Groups[1].Value;

            if (Regex.IsMatch(url, @"^\d+$")) return url;
            return "";
        }

        // 3. Bóc tách Post ID (Đã nâng cấp Regex cực mạnh để bắt mọi loại pfbid)
        public static string ExtractPostIdFromLink(string url) {
            try {
                // Bắt mọi ký tự sau story_fbid= cho đến khi gặp dấu & hoặc ?
                var matchFbid = Regex.Match(url, @"story_fbid=([^&?]+)");
                if (matchFbid.Success) return matchFbid.Groups[1].Value;

                // Bắt mọi ký tự sau /posts/ cho đến khi gặp dấu / hoặc ?
                var matchPost = Regex.Match(url, @"\/posts\/([^/?&]+)");
                if (matchPost.Success) return matchPost.Groups[1].Value;

                var matchVideo = Regex.Match(url, @"\/videos\/([^/?&]+)");
                if (matchVideo.Success) return matchVideo.Groups[1].Value;

                var matchPhoto = Regex.Match(url, @"\/photos\/[a-zA-Z0-9\.\-_]+\/([^/?&]+)");
                if (matchPhoto.Success) return matchPhoto.Groups[1].Value;

                var matchEnd = Regex.Match(url, @"\/([0-9]+)\/?(?:\?|$)");
                if (matchEnd.Success && matchEnd.Groups[1].Value.Length > 10) return matchEnd.Groups[1].Value;
            } catch { }
            return "";
        }
        public static string ConvertToPermalink(string url) {
            string pageId = ExtractPageIdFromLink(url);
            string postId = ExtractPostIdFromLink(url);
            string commentId = ExtractCommentId(url);

            // Kiểm tra bóc tách thành công
            if (!string.IsNullOrEmpty(pageId) && !string.IsNullOrEmpty(postId) && !string.IsNullOrEmpty(commentId)) {

                // Đã sắp xếp lại đúng chuẩn: story_fbid -> id -> comment_id
                return $"https://www.facebook.com/permalink.php?story_fbid={postId}&id={pageId}&comment_id={commentId}";
            }

            // Nếu thiếu ID, trả về link gốc
            return url;
        }
        // Hàm mới: Trích xuất định danh (Username hoặc ID) để làm khóa sắp xếp
        public static string GetSortingKey(string url) {
            if (string.IsNullOrEmpty(url)) return "";

            // 1. Nếu là dạng permalink có chứa tham số id=
            var matchId = Regex.Match(url, @"[?&]id=(\d+)");
            if (matchId.Success) return matchId.Groups[1].Value;

            // 2. Nếu là dạng link chứa username hoặc ID ngay sau facebook.com/
            var matchUser = Regex.Match(url, @"facebook\.com\/([^\/\?]+)");
            if (matchUser.Success) {
                string val = matchUser.Groups[1].Value;
                // Bỏ qua nếu nó bắt nhầm vào chữ permalink.php hoặc story.php
                if (val != "permalink.php" && val != "story.php" && val != "groups") {
                    return val.ToLower(); // Chuyển về chữ thường để sắp xếp chuẩn A-Z
                }
            }

            return url; // Nếu không bóc được gì thì trả về nguyên link
        }
    }
}