// Copyright Karel Kroeze, 2018-2021.
// ColorPicker/ColorPicker/Dialog_ColorPicker.cs

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Object = UnityEngine.Object;

namespace ilyvion.LoadingProgress.ColorPicker;

[SuppressMessage("Style", "IDE0032:Use auto property", Justification = "<Pending>")]
internal sealed class Dialog_ColorPicker : Window
{
    private Controls _activeControl = Controls.none;

    private Color _alphaBGColorA = Color.white,
        _alphaBGColorB = new(.85f, .85f, .85f);

    private readonly Action<Color>? _callback;

    private Texture2D? _ColorPickerBG,
        _huePickerBG,
        _alphaPickerBG,
        _tempPreviewBG,
        _previewBG,
        _pickerAlphaBG,
        _sliderAlphaBG,
        _previewAlphaBG;

    private string? _hex;

    private Vector2? _initialPosition;
    private readonly float _margin = 6f;
    private readonly float _buttonHeight = 30f;
    private readonly float _fieldHeight = 24f;
    private float _huePosition;
    private float _alphaPosition;
    private float _unitsPerPixel;
    private float _h;
    private float _s;
    private float _v;
    private readonly int _pickerSize = 300,
        _sliderWidth = 15,
        _alphaBGBlockSize = 10,
        _previewSize = 90, // odd multiple of alphaBGblocksize forces alternation of the background texture grid.
        _handleSize = 10,
        _recentSize = 20;

    private Vector2 _position = Vector2.zero;

    // used in the picker only
    private Color _tempColor;

    // the Color we're going to pass out if requested
    public Color curColor;
    private readonly TextField<string?> HexField;

    private readonly TextField<float> RedField,
        GreenField,
        BlueField,
        HueField,
        SaturationField,
        ValueField,
        Alpha1Field,
        Alpha2Field;

    private readonly List<string> textFieldIds;

    /// <summary>
    ///     Call with the current Color, and a callback which will be passed the new Color when 'OK' or 'Apply' is pressed.
    ///     Optionally, the Color pickers' position can be provided.
    /// </summary>
    /// <param name="color">The current Color</param>
    /// <param name="callback">Callback to be invoked with the selected Color when 'OK' or 'Apply' are pressed'</param>
    /// <param name="position">Top left position of the Color picker (defaults to screen center)</param>
    public Dialog_ColorPicker(Color color, Action<Color>? callback = null, Vector2? position = null)
    {
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;

        _callback = callback;
        _initialPosition = position;

        curColor = color;
        TempColor = color;

        HueField = TextField<float>.Float01(H, "Hue", h => H = h);
        SaturationField = TextField<float>.Float01(S, "Saturation", s => S = s);
        ValueField = TextField<float>.Float01(V, "Value", v => V = v);
        Alpha1Field = TextField<float>.Float01(A, "Alpha1", a => A = a);
        RedField = TextField<float>.Float01(color.r, "Red", r => R = r);
        GreenField = TextField<float>.Float01(color.g, "Green", g => G = g);
        BlueField = TextField<float>.Float01(color.b, "Blue", b => B = b);
        Alpha2Field = TextField<float>.Float01(A, "Alpha2", a => A = a);
        HexField = TextField<string>.Hex(Hex, "Hex", hex => Hex = hex);

        textFieldIds =
        [
            "Hue",
            "Saturation",
            "Value",
            "Alpha1",
            "Red",
            "Green",
            "Blue",
            "Alpha2",
            "Hex",
        ];

        NotifyRGBUpdated();
    }

    public float A
    {
        get => TempColor.a;
        set
        {
            var color = TempColor;
            color.a = Mathf.Clamp(value, 0f, 1f);
            TempColor = color;
            NotifyRGBUpdated();
        }
    }

    public Texture2D AlphaPickerBG
    {
        get
        {
            if (_alphaPickerBG == null)
            {
                CreateAlphaPickerBG();
            }

            return _alphaPickerBG;
        }
    }

    public float B
    {
        get => TempColor.b;
        set
        {
            var color = TempColor;
            color.b = Mathf.Clamp(value, 0f, 1f);
            TempColor = color;
            NotifyRGBUpdated();
        }
    }

    public Texture2D ColorPickerBG
    {
        get
        {
            if (_ColorPickerBG == null)
            {
                CreateColorPickerBG();
            }

            return _ColorPickerBG;
        }
    }

    public float G
    {
        get => TempColor.g;
        set
        {
            var color = TempColor;
            color.g = Mathf.Clamp(value, 0f, 1f);
            TempColor = color;
            NotifyRGBUpdated();
        }
    }

    public float H
    {
        get => _h;
        set
        {
            _h = Mathf.Clamp(value, 0f, 1f);
            NotifyHSVUpdated();
            CreateColorPickerBG();
            CreateAlphaPickerBG();
        }
    }

    public string? Hex
    {
        get => $"#{ColorUtility.ToHtmlStringRGBA(TempColor)}";
        set
        {
            _hex = value;
            NotifyHexUpdated();
        }
    }

    public Texture2D HuePickerBG
    {
        get
        {
            if (_huePickerBG == null)
            {
                CreateHuePickerBG();
            }

            return _huePickerBG;
        }
    }

    public Vector2 InitialPosition =>
        _initialPosition
        ?? (new Vector2(UI.screenWidth - InitialSize.x, UI.screenHeight - InitialSize.y) / 2f);

    public override Vector2 InitialSize =>
        // calculate window size to accomodate all elements
        new(
            _pickerSize
                + (3 * _margin)
                + (2 * _sliderWidth)
                + (2 * _previewSize)
                + (StandardMargin * 2),
            _pickerSize + (StandardMargin * 2)
        );

    public Texture2D PickerAlphaBG
    {
        get
        {
            if (_pickerAlphaBG == null)
            {
                CreateAlphaBG(ref _pickerAlphaBG, _pickerSize, _pickerSize);
            }

            return _pickerAlphaBG;
        }
    }

    public Texture2D PreviewAlphaBG
    {
        get
        {
            if (_previewAlphaBG == null)
            {
                CreateAlphaBG(ref _previewAlphaBG, _previewSize, _previewSize);
            }

            return _previewAlphaBG;
        }
    }

    public Texture2D PreviewBG
    {
        get
        {
            if (_previewBG == null)
            {
                CreatePreviewBG(ref _previewBG, curColor);
            }

            return _previewBG;
        }
    }

    public float R
    {
        get => TempColor.r;
        set
        {
            var color = TempColor;
            color.r = Mathf.Clamp(value, 0f, 1f);
            TempColor = color;
            NotifyRGBUpdated();
        }
    }

    public float S
    {
        get => _s;
        set
        {
            _s = Mathf.Clamp(value, 0f, 1f);
            NotifyHSVUpdated();
            CreateAlphaPickerBG();
        }
    }

    public Texture2D SliderAlphaBG
    {
        get
        {
            if (_sliderAlphaBG == null)
            {
                CreateAlphaBG(ref _sliderAlphaBG, _sliderWidth, _pickerSize);
            }

            return _sliderAlphaBG;
        }
    }

    public Color TempColor
    {
        get => _tempColor;
        set => _tempColor = value;
    }

    public Texture2D TempPreviewBG
    {
        get
        {
            if (_tempPreviewBG == null)
            {
                CreatePreviewBG(ref _tempPreviewBG, TempColor);
            }

            return _tempPreviewBG;
        }
    }

    public float UnitsPerPixel
    {
        get
        {
            if (_unitsPerPixel == 0.0f)
            {
                _unitsPerPixel = 1f / _pickerSize;
            }

            return _unitsPerPixel;
        }
    }

    public float V
    {
        get => _v;
        set
        {
            _v = Mathf.Clamp(value, 0f, 1f);
            NotifyHSVUpdated();
            CreateAlphaPickerBG();
        }
    }

    public void AlphaAction(float pos)
    {
        // only changing one value, property should work fine
        A = 1 - (UnitsPerPixel * pos);
        _alphaPosition = pos;
    }

    private void CreateAlphaBG([NotNull] ref Texture2D? bg, int width, int height)
    {
        var tex = new Texture2D(width, height);

        // initialize color arrays for blocks
        var bgA = new Color[_alphaBGBlockSize * _alphaBGBlockSize];
        for (var i = 0; i < bgA.Length; i++)
        {
            bgA[i] = _alphaBGColorA;
        }

        var bgB = new Color[_alphaBGBlockSize * _alphaBGBlockSize];
        for (var i = 0; i < bgB.Length; i++)
        {
            bgB[i] = _alphaBGColorB;
        }

        // set blocks of pixels at a time
        // this also sets border blocks, meaning it'll try to set out of bounds pixels.
        var row = 0;
        for (var x = 0; x < width - _alphaBGBlockSize; x += _alphaBGBlockSize)
        {
            var column = row;
            for (var y = 0; y < height - _alphaBGBlockSize; y += _alphaBGBlockSize)
            {
                tex.SetPixels(
                    x,
                    y,
                    _alphaBGBlockSize,
                    _alphaBGBlockSize,
                    column % 2 == 0 ? bgA : bgB
                );
                column++;
            }

            row++;
        }

        tex.Apply();
        SwapTexture(ref bg, tex);
    }

    [MemberNotNull(nameof(_alphaPickerBG))]
    private void CreateAlphaPickerBG()
    {
        var tex = new Texture2D(1, _pickerSize);
        Object.Destroy(_alphaPickerBG);
        _alphaPickerBG = tex;

        var h = _pickerSize;
        var hu = 1f / h;

        // RGB color from cache, alternate a
        for (var y = 0; y < h; y++)
        {
            tex.SetPixel(0, y, new Color(TempColor.r, TempColor.g, TempColor.b, y * hu));
        }

        tex.Apply();
    }

    [MemberNotNull(nameof(_ColorPickerBG))]
    private void CreateColorPickerBG()
    {
        float S,
            V;
        var w = _pickerSize;
        var h = _pickerSize;
        var wu = UnitsPerPixel;
        var hu = UnitsPerPixel;

        var tex = new Texture2D(w, h);
        Object.Destroy(_ColorPickerBG);
        _ColorPickerBG = tex;

        // HSV Colors, H in slider, S horizontal, V vertical.
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                S = x * wu;
                V = y * hu;
                tex.SetPixel(x, y, HSVAToRGB(H, S, V, A));
            }
        }

        tex.Apply();
    }

    [MemberNotNull(nameof(_huePickerBG))]
    private void CreateHuePickerBG()
    {
        var tex = new Texture2D(1, _pickerSize);
        Object.Destroy(_huePickerBG);
        _huePickerBG = tex;

        var h = _pickerSize;
        var hu = 1f / h;

        // HSV Colors, S = V = 1
        for (var y = 0; y < h; y++)
        {
            tex.SetPixel(0, y, Color.HSVToRGB(hu * y, 1f, 1f));
        }

        tex.Apply();
    }

    public static void CreatePreviewBG([NotNull] ref Texture2D? bg, Color col) =>
        SwapTexture(ref bg, SolidColorMaterials.NewSolidColorTexture(col));

    [Conditional("DEBUG")]
    public static void Debug(string msg)
    {
        if (Traverse.Create(typeof(Log)).Field("reachedMaxMessagesLimit").GetValue<bool>())
        {
            Log.ResetMessageCount();
        }

        Log.Message($"ColorPicker :: {msg}");
    }

    public override void DoWindowContents(Rect inRect)
    {
        // set up rects
        var pickerRect = new Rect(inRect.xMin, inRect.yMin, _pickerSize, _pickerSize);
        var hueRect = new Rect(pickerRect.xMax + _margin, inRect.yMin, _sliderWidth, _pickerSize);
        var alphaRect = new Rect(hueRect.xMax + _margin, inRect.yMin, _sliderWidth, _pickerSize);
        var previewRect = new Rect(
            alphaRect.xMax + _margin,
            inRect.yMin,
            _previewSize,
            _previewSize
        );
        var previewOldRect = new Rect(previewRect.xMax, inRect.yMin, _previewSize, _previewSize);
        var doneRect = new Rect(
            alphaRect.xMax + _margin,
            inRect.yMax - _buttonHeight,
            _previewSize * 2,
            _buttonHeight
        );
        var setRect = new Rect(
            alphaRect.xMax + _margin,
            inRect.yMax - (2 * _buttonHeight) - _margin,
            _previewSize - (_margin / 2),
            _buttonHeight
        );
        var cancelRect = new Rect(
            setRect.xMax + _margin,
            setRect.yMin,
            _previewSize - (_margin / 2),
            _buttonHeight
        );
        var hsvFieldRect = new Rect(
            alphaRect.xMax + _margin,
            inRect.yMax - (2 * _buttonHeight) - (3 * _fieldHeight) - (4 * _margin),
            _previewSize * 2,
            _fieldHeight
        );
        var rgbFieldRect = new Rect(
            alphaRect.xMax + _margin,
            inRect.yMax - (2 * _buttonHeight) - (2 * _fieldHeight) - (3 * _margin),
            _previewSize * 2,
            _fieldHeight
        );
        var hexRect = new Rect(
            alphaRect.xMax + _margin,
            inRect.yMax - (2 * _buttonHeight) - (1 * _fieldHeight) - (2 * _margin),
            _previewSize * 2,
            _fieldHeight
        );
        var recentRect = new Rect(
            previewRect.xMin,
            previewRect.yMax + _margin,
            _previewSize * 2,
            _recentSize * 2
        );

        // draw transparency backgrounds
        GUI.DrawTexture(pickerRect, PickerAlphaBG);
        GUI.DrawTexture(alphaRect, SliderAlphaBG);
        GUI.DrawTexture(previewRect, PreviewAlphaBG);
        GUI.DrawTexture(previewOldRect, PreviewAlphaBG);

        // draw picker foregrounds
        GUI.DrawTexture(pickerRect, ColorPickerBG);
        GUI.DrawTexture(hueRect, HuePickerBG);
        GUI.DrawTexture(alphaRect, AlphaPickerBG);
        GUI.DrawTexture(previewRect, TempPreviewBG);
        GUI.DrawTexture(previewOldRect, PreviewBG);

        if (Widgets.ButtonInvisible(previewOldRect))
        {
            TempColor = curColor;
            NotifyRGBUpdated();
        }

        // draw recent Colors
        DrawRecent(recentRect);

        // draw slider handles
        var hueHandleRect = new Rect(
            hueRect.xMin - 3f,
            hueRect.yMin + _huePosition - (_handleSize / 2),
            _sliderWidth + 6f,
            _handleSize
        );
        var alphaHandleRect = new Rect(
            alphaRect.xMin - 3f,
            alphaRect.yMin + _alphaPosition - (_handleSize / 2),
            _sliderWidth + 6f,
            _handleSize
        );
        var pickerHandleRect = new Rect(
            pickerRect.xMin + _position.x - (_handleSize / 2),
            pickerRect.yMin + _position.y - (_handleSize / 2),
            _handleSize,
            _handleSize
        );
        GUI.DrawTexture(hueHandleRect, TempPreviewBG);
        GUI.DrawTexture(alphaHandleRect, TempPreviewBG);
        GUI.DrawTexture(pickerHandleRect, TempPreviewBG);

        GUI.color = Color.gray;
        Widgets.DrawBox(hueHandleRect);
        Widgets.DrawBox(alphaHandleRect);
        Widgets.DrawBox(pickerHandleRect);
        GUI.color = Color.white;

        // reset active control on mouseup
        if (Input.GetMouseButtonUp(0))
        {
            _activeControl = Controls.none;
        }

        DrawColorPicker(pickerRect);
        DrawHuePicker(hueRect);
        DrawAlphaPicker(alphaRect);
        DrawFields(hsvFieldRect, rgbFieldRect, hexRect);
        DrawButtons(doneRect, setRect, cancelRect);

        GUI.color = Color.white;
    }

    private void DrawAlphaPicker(Rect alphaRect)
    {
        // alpha picker interaction
        if (Mouse.IsOver(alphaRect))
        {
            if (Input.GetMouseButtonDown(0))
            {
                _activeControl = Controls.alphaPicker;
            }

            if (Event.current.type == EventType.ScrollWheel)
            {
                A -= Event.current.delta.y * UnitsPerPixel;
                _alphaPosition = Mathf.Clamp(
                    _alphaPosition + Event.current.delta.y,
                    0f,
                    _pickerSize
                );
                Event.current.Use();
            }

            if (_activeControl == Controls.alphaPicker)
            {
                var MousePosition = Event.current.mousePosition.y;
                var PositionInRect = MousePosition - alphaRect.yMin;

                AlphaAction(PositionInRect);
            }
        }
    }

    private void DrawButtons(Rect doneRect, Rect setRect, Rect cancelRect)
    {
        if (Widgets.ButtonText(doneRect, "OK"))
        {
            SetColor();
            Close();
        }

        if (Widgets.ButtonText(setRect, "Apply"))
        {
            SetColor();
        }

        if (Widgets.ButtonText(cancelRect, "Cancel"))
        {
            Close();
        }
    }

    private void DrawColorPicker(Rect pickerRect)
    {
        // Colorpicker interaction
        if (Mouse.IsOver(pickerRect))
        {
            if (Input.GetMouseButtonDown(0))
            {
                _activeControl = Controls.ColorPicker;
            }

            if (_activeControl == Controls.ColorPicker)
            {
                var MousePosition = Event.current.mousePosition;
                var PositionInRect = MousePosition - new Vector2(pickerRect.xMin, pickerRect.yMin);

                PickerAction(PositionInRect);
            }
        }
    }

    private void DrawFields(Rect hsvFieldRect, Rect rgbFieldRect, Rect hexRect)
    {
        Text.Font = GameFont.Small;

        var fieldRect = hsvFieldRect;
        fieldRect.width /= 5f;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.grey;
        Widgets.Label(fieldRect, "HSV");
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        fieldRect.x += fieldRect.width;
        HueField.Draw(fieldRect);
        fieldRect.x += fieldRect.width;
        SaturationField.Draw(fieldRect);
        fieldRect.x += fieldRect.width;
        ValueField.Draw(fieldRect);
        fieldRect.x += fieldRect.width;
        Alpha1Field.Draw(fieldRect);

        fieldRect = rgbFieldRect;
        fieldRect.width /= 5f;
        Text.Font = GameFont.Tiny;
        GUI.color = Color.grey;
        Widgets.Label(fieldRect, "RGB");
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        fieldRect.x += fieldRect.width;
        RedField.Draw(fieldRect);
        fieldRect.x += fieldRect.width;
        GreenField.Draw(fieldRect);
        fieldRect.x += fieldRect.width;
        BlueField.Draw(fieldRect);
        fieldRect.x += fieldRect.width;
        Alpha2Field.Draw(fieldRect);

        Text.Font = GameFont.Tiny;
        GUI.color = Color.grey;
        Widgets.Label(new Rect(hexRect.xMin, hexRect.yMin, fieldRect.width, hexRect.height), "HEX");
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        hexRect.xMin += fieldRect.width;
        HexField.Draw(hexRect);
        Text.Anchor = TextAnchor.UpperLeft;

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
        {
            var curControl = GUI.GetNameOfFocusedControl();
            var curControlIndex = textFieldIds.IndexOf(curControl);
            GUI.FocusControl(
                textFieldIds[
                    GenMath.PositiveMod(
                        curControlIndex + (Event.current.shift ? -1 : 1),
                        textFieldIds.Count
                    )
                ]
            );
        }
    }

    private void DrawHuePicker(Rect hueRect)
    {
        // hue picker interaction
        if (Mouse.IsOver(hueRect))
        {
            if (Input.GetMouseButtonDown(0))
            {
                _activeControl = Controls.huePicker;
            }

            if (Event.current.type == EventType.ScrollWheel)
            {
                H -= Event.current.delta.y * UnitsPerPixel;
                _huePosition = Mathf.Clamp(_huePosition + Event.current.delta.y, 0f, _pickerSize);
                Event.current.Use();
            }

            if (_activeControl == Controls.huePicker)
            {
                var MousePosition = Event.current.mousePosition.y;
                var PositionInRect = MousePosition - hueRect.yMin;

                HueAction(PositionInRect);
            }
        }
    }

    private void DrawRecent(Rect canvas)
    {
        var cols = (int)(canvas.width / _recentSize);
        var rows = (int)(canvas.height / _recentSize);
        var n = Math.Min(cols * rows, RecentColors.Count);

        GUI.BeginGroup(canvas);
        for (var i = 0; i < n; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var color = RecentColors.Colors[i];
            var rect = new Rect(col * _recentSize, row * _recentSize, _recentSize, _recentSize);
            Widgets.DrawBoxSolid(rect, color);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawBox(rect);
            }

            if (Widgets.ButtonInvisible(rect))
            {
                TempColor = color;
                NotifyRGBUpdated();
            }
        }

        GUI.EndGroup();
    }

    public static Color HSVAToRGB(float H, float S, float V, float A)
    {
        var color = Color.HSVToRGB(H, S, V);
        color.a = A;
        return color;
    }

    public void HueAction(float pos)
    {
        // only changing one value, property should work fine
        H = 1 - (UnitsPerPixel * pos);
        _huePosition = pos;
    }

    public void NotifyHexUpdated()
    {
        Debug($"HEX updated ({Hex})");

        if (ColorUtility.TryParseHtmlString(_hex, out var color))
        {
            // set rgb Color;
            TempColor = color;

            // do all the rgb update actions
            NotifyRGBUpdated();

            // also set RGB text fields
            RedField.Value = TempColor.r;
            GreenField.Value = TempColor.g;
            BlueField.Value = TempColor.b;
        }
    }

    public void NotifyHSVUpdated()
    {
        Debug($"HSV updated: ({_h}, {_s}, {_v})");

        // update rgb Color
        var color = Color.HSVToRGB(H, S, V);
        color.a = A;
        TempColor = color;

        // set the Color block
        CreatePreviewBG(ref _tempPreviewBG, TempColor);
        SetPickerPositions();

        // update text fields
        RedField.Value = TempColor.r;
        GreenField.Value = TempColor.g;
        BlueField.Value = TempColor.b;
        HueField.Value = H;
        SaturationField.Value = S;
        ValueField.Value = V;
        Alpha1Field.Value = A;
        Alpha2Field.Value = A;
        HexField.Value = Hex;
    }

    public void NotifyRGBUpdated()
    {
        Debug($"RGB updated: ({R}, {G}, {B})");

        // Set HSV from RGB
        Color.RGBToHSV(TempColor, out _h, out _s, out _v);

        // rebuild textures
        CreateColorPickerBG();
        CreateHuePickerBG();
        CreateAlphaPickerBG();

        // set the Color block
        CreatePreviewBG(ref _tempPreviewBG, TempColor);
        SetPickerPositions();

        // udpate text fields
        HueField.Value = H;
        SaturationField.Value = S;
        ValueField.Value = V;
        Alpha1Field.Value = A;
        Alpha2Field.Value = A;
        HexField.Value = Hex;
    }

    public override void OnAcceptKeyPressed()
    {
        base.OnAcceptKeyPressed();
        SetColor();
    }

    public void PickerAction(Vector2 pos)
    {
        // if we set S, V via properties these will be called twice.
        _s = UnitsPerPixel * pos.x;
        _v = 1 - (UnitsPerPixel * pos.y);

        CreateAlphaPickerBG();
        NotifyHSVUpdated();
        _position = pos;
    }

    public override void PreOpen()
    {
        base.PreOpen();
        NotifyHSVUpdated();
    }

    public void SetColor()
    {
        curColor = TempColor;
        RecentColors.Add(TempColor);
        _callback?.Invoke(curColor);
        CreatePreviewBG(ref _previewBG, TempColor);
    }

    protected override void SetInitialSizeAndPosition()
    {
        // get position based on requested size and position, limited by screen space.
        var size = new Vector2(
            Mathf.Min(InitialSize.x, UI.screenWidth),
            Mathf.Min(InitialSize.y, UI.screenHeight - 35f)
        );

        var position = new Vector2(
            Mathf.Max(0f, Mathf.Min(InitialPosition.x, UI.screenWidth - size.x)),
            Mathf.Max(0f, Mathf.Min(InitialPosition.y, UI.screenHeight - size.y))
        );

        windowRect = new Rect(position.x, position.y, size.x, size.y);
    }

    public void SetPickerPositions()
    {
        // set slider positions
        _huePosition = (1f - H) / UnitsPerPixel;
        _position.x = S / UnitsPerPixel;
        _position.y = (1f - V) / UnitsPerPixel;
        _alphaPosition = (1f - A) / UnitsPerPixel;
    }

    private static void SwapTexture([NotNull] ref Texture2D? tex, Texture2D newTex)
    {
        Object.Destroy(tex);
        tex = newTex;
    }

    private enum Controls
    {
        ColorPicker,
        huePicker,
        alphaPicker,
        none,
    }
}
