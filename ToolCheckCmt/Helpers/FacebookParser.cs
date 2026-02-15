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

        public static string ExtractPostIdFromLink(string url) {
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
    }
}