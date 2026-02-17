using System.Text.RegularExpressions;

namespace ToolCheckCmt {
    public static class FacebookParser {

        // 1. Bóc Page ID (Đã fix lỗi bắt id= trong permalink)
        public static string ExtractPageIdFromLink(string url) {
            var match = Regex.Match(url, @"facebook\.com\/(\d+)");
            if (match.Success) return match.Groups[1].Value;

            var matchIdParam = Regex.Match(url, @"[?&]id=(\d+)");
            if (matchIdParam.Success) return matchIdParam.Groups[1].Value;

            return null;
        }

        // 2. Bóc Comment ID
        public static string ExtractCommentId(string url) {
            var match = Regex.Match(url, @"comment_id=(\d+)");
            if (match.Success) return match.Groups[1].Value;

            var match2 = Regex.Match(url, @"reply_comment_id=(\d+)");
            if (match2.Success) return match2.Groups[1].Value;

            if (Regex.IsMatch(url, @"^\d+$")) return url;
            return "";
        }

        // 3. Bóc Post ID (Bắt mọi thể loại pfbid, posts, videos...)
        public static string ExtractPostIdFromLink(string url) {
            try {
                var matchFbid = Regex.Match(url, @"story_fbid=([^&?]+)");
                if (matchFbid.Success) return matchFbid.Groups[1].Value;

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

        // 4. Hàm ráp link SẠCH BONG (Không chứa tracking cft hay tn)
        public static string ConvertToCleanPermalink(string url, string username = null) {
            string pageId = ExtractPageIdFromLink(url);
            string postId = ExtractPostIdFromLink(url);
            string commentId = ExtractCommentId(url);

            if (!string.IsNullOrEmpty(postId) && !string.IsNullOrEmpty(commentId)) {

                // TRƯỜNG HỢP 1: Có Username bằng chữ
                if (!string.IsNullOrEmpty(username) && username != "NO_USER" && !Regex.IsMatch(username, @"^\d+$")) {
                    return $"https://www.facebook.com/{username}/posts/{postId}?comment_id={commentId}";
                }

                // TRƯỜNG HỢP 2: Dạng Permalink ID số
                if (!string.IsNullOrEmpty(pageId)) {
                    return $"https://www.facebook.com/permalink.php?story_fbid={postId}&id={pageId}&comment_id={commentId}";
                }
            }
            return url;
        }

        // 5. Khóa sắp xếp ngẫu nhiên gom Page lúc xuất Excel
        public static string GetSortingKey(string url) {
            if (string.IsNullOrEmpty(url)) return "";
            var matchId = Regex.Match(url, @"[?&]id=(\d+)");
            if (matchId.Success) return matchId.Groups[1].Value;

            var matchUser = Regex.Match(url, @"facebook\.com\/([^\/\?]+)");
            if (matchUser.Success) {
                string val = matchUser.Groups[1].Value;
                if (val != "permalink.php" && val != "story.php" && val != "groups") {
                    return val.ToLower();
                }
            }
            return url;
        }
    }
}