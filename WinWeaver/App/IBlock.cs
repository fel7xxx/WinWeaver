namespace WinWeaver;

internal interface IBlock
{
	int DoLayout(int x, int y, int w);

	object Fetch(IEngine e);

	void Apply(object v);
}
