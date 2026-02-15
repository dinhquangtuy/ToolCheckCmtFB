using System.Drawing;

namespace ToolCheckCmt {
    public class ResultModel {
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
}