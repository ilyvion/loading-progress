namespace ilyvion.LoadingProgress.StartupImpact;

[HotSwappable]
internal sealed class UiTable(int rowCount, float rowHeight, float[] columnWidthsTemplate)
{
    private readonly int _rowCount = rowCount;
    private readonly float _rowHeight = rowHeight;
    private readonly float[] _columnWidthsTemplate = columnWidthsTemplate;
    private readonly float[] _columnOffsets = new float[columnWidthsTemplate.Length];
    private readonly float[] _columnWidths = new float[columnWidthsTemplate.Length];
    private Rect _uiRect;
    private Rect _viewRect;
    private Rect _userRect;
    private Vector2 _scrollPosition = Vector2.zero;

    public void StartTable(float x, float y, float width, float height)
    {
        if (_uiRect.x != x || _uiRect.y != y || _uiRect.width != width || _uiRect.height != height)
        {
            _uiRect.x = x;
            _uiRect.y = y;
            _uiRect.width = width;
            _uiRect.height = height;

            _viewRect.x = 0;
            _viewRect.y = 0;
            _viewRect.width = width - 16f;
            _viewRect.height = _rowCount * _rowHeight;

            float totalNeededWidth = 0;
            var totalAvailableWidth = _viewRect.width;
            foreach (var cw in _columnWidthsTemplate)
            {
                if (cw > 0)
                {
                    totalNeededWidth += cw;
                }
                else
                {
                    totalAvailableWidth += cw;
                }
            }

            float xoff = 0;
            var n = 0;
            foreach (var cw in _columnWidthsTemplate)
            {
                var calculatedWidth = cw > 0
                    ? cw * totalAvailableWidth / totalNeededWidth
                    : -cw;
                _columnOffsets[n] = xoff;
                _columnWidths[n] = calculatedWidth;

                xoff += calculatedWidth;
                n++;
            }

        }

        Widgets.BeginScrollView(_uiRect, ref _scrollPosition, _viewRect);
    }

    public bool IsRowVisible(int row)
    {
        // visible area
        Rect viewRect = new(0f, _scrollPosition.y, _uiRect.width, _uiRect.height);

        return Cell(0, row).Overlaps(viewRect);
    }

    public Rect Cell(int column, int row)
    {
        if (column < 0 || column >= _columnWidths.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(column), $"Bad column coordinate: {column}");
        }
        if (row < 0 || row >= _rowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row), $"Bad row coordinate: {row}");
        }

        _userRect.x = _columnOffsets[column];
        _userRect.y = row * _rowHeight;
        _userRect.width = _columnWidths[column];
        _userRect.height = _rowHeight;

        return _userRect;
    }

    public void TruncatedLabel(int column, int row, string text)
    {
        var rect = Cell(column, row);
        Widgets.Label(rect, text.Truncate(rect.width, null));
    }

#pragma warning disable CA1822 // Mark members as static
    public void EndTable() => Widgets.EndScrollView();
#pragma warning restore CA1822 // Mark members as static
}
