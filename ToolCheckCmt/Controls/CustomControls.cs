using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ToolCheckCmt {
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