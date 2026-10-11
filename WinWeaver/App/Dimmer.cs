
namespace WinWeaver;

internal class Dimmer : Form
{
	protected override bool ShowWithoutActivation => true;

	public Dimmer(Form owner)
	{
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.Manual;
		BackColor = Color.Black;
		base.Opacity = 0.45;
		base.Bounds = owner.RectangleToScreen(owner.ClientRectangle);
		base.AutoScaleMode = AutoScaleMode.None;
	}
}
