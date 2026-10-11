using System;

namespace WinWeaver;

internal class Card : Control, IBlock, ISurface
{
	public bool Hot;

	public bool HoverFx;

	protected readonly Tween HotT;

	protected int Pad => Theme.S(20f);

	public Card()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		ForeColor = Theme.Text;
		HotT = new Tween(this, 0f, 0.22f, delegate
		{
			Invalidate(invalidateChildren: true);
		});
	}

	protected void SetHot(bool hot)
	{
		Hot = hot;
		HotT.Target = (hot ? 1f : 0f);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		if (HoverFx && !Page.Scrolling)
		{
			SetHot(hot: true);
		}
		base.OnMouseEnter(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (HoverFx && !Hot && !Page.Scrolling)
		{
			SetHot(hot: true);
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		LeaveCheck();
		base.OnMouseLeave(e);
	}

	private void LeaveCheck()
	{
		if (HoverFx && Hot && !base.IsDisposed && !base.ClientRectangle.Contains(PointToClient(Cursor.Position)))
		{
			SetHot(hot: false);
		}
	}

	protected override void OnControlAdded(ControlEventArgs e)
	{
		e.Control.MouseLeave += ChildLeave;
		base.OnControlAdded(e);
	}

	protected override void OnControlRemoved(ControlEventArgs e)
	{
		e.Control.MouseLeave -= ChildLeave;
		base.OnControlRemoved(e);
	}

	private void ChildLeave(object sender, EventArgs e)
	{
		LeaveCheck();
	}

	protected override void OnVisibleChanged(EventArgs e)
	{
		if (!base.Visible && Hot)
		{
			Hot = false;
			HotT.Snap(0f);
		}
		base.OnVisibleChanged(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		PaintSurface(e.Graphics);
		PaintContent(e.Graphics);
	}

	public virtual void PaintSurface(Graphics g)
	{
		Theme.Card(g, base.ClientRectangle, HotT.Value);
	}

	protected virtual void PaintContent(Graphics g)
	{
	}

	public virtual int DoLayout(int x, int y, int w)
	{
		SetBounds(x, y, w, base.Height);
		return base.Height;
	}

	public virtual object Fetch(IEngine e)
	{
		return null;
	}

	public virtual void Apply(object v)
	{
	}
}
