using System.Globalization;
using System.Numerics;
using System.Text;
using Vellum.Rendering;

namespace Vellum;

public sealed partial class Ui
{
    private sealed class TextFieldState
    {
        public int CaretIndex;
        public int SelectionAnchor;
        public float ScrollX;
        public float ScrollY;
        public float PreferredCaretX;
        public bool HasPreferredCaretX;
        public bool Editing;
        public string EditStartText = string.Empty;
    }

    private sealed class TextAreaLine
    {
        public required string Text;
        public required int StartGrapheme;
        public required int GraphemeCount;
        public required bool HasLineBreak;
        public required float Y;
        public required TextLineMetrics Metrics;
        public required TextLayoutResult Layout;

        public int EndGrapheme => StartGrapheme + GraphemeCount;
        public int NextLineStartGrapheme => EndGrapheme + (HasLineBreak ? 1 : 0);
    }

    private sealed class TextAreaMetrics
    {
        private readonly TextAreaLine[] _lines;

        public TextAreaMetrics(TextAreaLine[] lines, float contentWidth, float contentHeight, float lineHeight, float lineAdvance)
        {
            _lines = lines;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
            LineHeight = lineHeight;
            LineAdvance = lineAdvance;
        }

        public ReadOnlySpan<TextAreaLine> Lines => _lines;
        public int LineCount => _lines.Length;
        public float ContentWidth { get; }
        public float ContentHeight { get; }
        public float LineHeight { get; }
        public float LineAdvance { get; }

        public int FindLineIndexForCaret(int graphemeIndex)
        {
            if (_lines.Length == 0)
                return 0;

            for (int i = 0; i < _lines.Length; i++)
            {
                if (graphemeIndex < _lines[i].NextLineStartGrapheme || i == _lines.Length - 1)
                    return i;
            }

            return _lines.Length - 1;
        }

        public Vector2 GetCaretPosition(int graphemeIndex)
        {
            int lineIndex = FindLineIndexForCaret(graphemeIndex);
            var line = _lines[lineIndex];
            int localIndex = Math.Clamp(graphemeIndex - line.StartGrapheme, 0, line.GraphemeCount);
            return new Vector2(line.Metrics.GetCaretX(localIndex), line.Y);
        }

        public int HitTest(float x, float y)
        {
            if (_lines.Length == 0)
                return 0;

            int lineIndex = 0;
            if (y > 0)
            {
                lineIndex = Math.Clamp((int)MathF.Floor(y / MathF.Max(1f, LineAdvance)), 0, _lines.Length - 1);
                while (lineIndex + 1 < _lines.Length && y >= _lines[lineIndex + 1].Y)
                    lineIndex++;
            }

            var line = _lines[lineIndex];
            return line.StartGrapheme + line.Metrics.HitTest(x);
        }

        public int MoveVertical(int graphemeIndex, float desiredX, int deltaLines)
        {
            int currentLineIndex = FindLineIndexForCaret(graphemeIndex);
            int targetLineIndex = Math.Clamp(currentLineIndex + deltaLines, 0, _lines.Length - 1);
            var targetLine = _lines[targetLineIndex];
            return targetLine.StartGrapheme + targetLine.Metrics.HitTest(desiredX);
        }

        public int GetLineStart(int graphemeIndex)
        {
            var line = _lines[FindLineIndexForCaret(graphemeIndex)];
            return line.StartGrapheme;
        }

        public int GetLineEnd(int graphemeIndex)
        {
            var line = _lines[FindLineIndexForCaret(graphemeIndex)];
            return line.EndGrapheme;
        }
    }

    private readonly struct TextInputOutcome
    {
        public readonly bool Changed;
        public readonly bool Submitted;
        public readonly bool Cancelled;

        public TextInputOutcome(bool changed, bool submitted, bool cancelled)
        {
            Changed = changed;
            Submitted = submitted;
            Cancelled = cancelled;
        }
    }

    /// <summary>Draws a single-line editable text field.</summary>
    public Response TextField(
        string id,
        ref string text,
        float width,
        float? size = null,
        string? placeholder = null,
        bool enabled = true,
        bool readOnly = false)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = Theme.TextFieldPadding;
        int widgetId = MakeId(id);
        float border = FrameBorderWidth;

        string normalizedText = SanitizeTextInput(text);
        if (normalizedText != text) text = normalizedText;

        var state = GetTextFieldState(widgetId);
        ClampTextFieldState(state, text);

        var metrics = MeasureTextLine(text, s);
        var textLayout = LayoutText(text, s);
        TextLayoutResult? placeholderLayout = null;

        float innerTextWidth = MathF.Max(0, width - border * 2 - pad.Horizontal);
        float h = metrics.Height + pad.Vertical + border * 2;
        var (x, y) = Place(width, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        if ((!focused || readOnly) && state.Editing)
            EndEditSession(state);

        bool hover = enabled && PointIn(x, y, width, h);
        if (hover) _hotId = widgetId;
        if (enabled && (hover || focused)) RequestCursor(UiCursor.IBeam);

        bool mousePressed = enabled && hover && IsMousePressed(UiMouseButton.Left);
        bool wasFocused = focused;

        if (mousePressed)
        {
            SetFocus(widgetId);
            _activeId = widgetId;
            focused = true;
            if (!wasFocused && !readOnly)
                BeginEditSession(state, text);

            int hit = metrics.HitTest(_mouse.X - (x + border + pad.Left) + state.ScrollX);
            if (!(_input.Shift && wasFocused))
                state.SelectionAnchor = hit;

            state.CaretIndex = hit;
        }
        else if (enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left))
        {
            SetFocus(widgetId);
            focused = true;
            state.CaretIndex = metrics.HitTest(_mouse.X - (x + border + pad.Left) + state.ScrollX);
        }
        else if (focused && !state.Editing && !readOnly)
        {
            BeginEditSession(state, text);
        }

        TextInputOutcome outcome = default;
        if (enabled && focused)
        {
            if (readOnly)
            {
                ApplyReadOnlyTextFieldInput(state, text);
            }
            else
            {
                outcome = ApplyTextFieldInput(widgetId, state, ref text);
                if (outcome.Changed)
                {
                    metrics = MeasureTextLine(text, s);
                    textLayout = LayoutText(text, s);
                }

                if (outcome.Submitted || outcome.Cancelled)
                    focused = false;
            }
        }

        ClampTextFieldState(state, text);
        EnsureCaretVisible(state, metrics, innerTextWidth);

        bool pressed = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        bool clicked = enabled && IsMouseReleased(UiMouseButton.Left) && _activeId == widgetId && hover;

        Color borderColor = !enabled ? Theme.TextFieldBorder.WithAlpha(140)
            : focused ? Theme.TextFieldBorderFocused
            : Theme.TextFieldBorder;
        Color bg = !enabled ? Theme.TextFieldBg.WithAlpha(180)
            : focused ? Theme.TextFieldBgFocused
            : hover ? Theme.TextFieldBgHover
            : Theme.TextFieldBg;

        DrawFrameRect(x, y, width, h, bg, borderColor);

        float textX = x + border + pad.Left;
        float textY = y + border + pad.Top;
        float clipHeight = metrics.Height;

        if (innerTextWidth > 0 && clipHeight > 0)
        {
            _painter.PushClip(textX, textY, innerTextWidth, clipHeight);

            if (HasSelection(state))
            {
                var (selectionStart, selectionEnd) = GetSelectionRange(state);
                float selectionX = textX + metrics.GetCaretX(selectionStart) - state.ScrollX;
                float selectionW = metrics.GetCaretX(selectionEnd) - metrics.GetCaretX(selectionStart);
                if (selectionW > 0)
                    _painter.DrawRect(selectionX, textY, selectionW, clipHeight, Theme.TextFieldSelectionBg);
            }

            if (text.Length == 0)
            {
                if (!string.IsNullOrEmpty(placeholder))
                {
                    placeholderLayout ??= LayoutText(placeholder, s);
                    DrawTextLayout(
                        placeholderLayout.Value,
                        textX,
                        textY,
                        enabled ? Theme.TextFieldPlaceholder : Theme.TextFieldPlaceholder.WithAlpha(140));
                }
            }
            else
            {
                DrawTextLayout(
                    textLayout,
                    textX - state.ScrollX,
                    textY,
                    enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140));
            }

            if (enabled && focused && !readOnly)
            {
                float caretX = textX + metrics.GetCaretX(state.CaretIndex) - state.ScrollX;
                _painter.DrawRect(caretX, textY, 1, clipHeight, Theme.TextFieldCaret);
            }

            _painter.PopClip();
        }

        Advance(width, h);
        return new Response(
            x,
            y,
            width,
            h,
            hover,
            pressed,
            clicked,
            focused: focused,
            changed: outcome.Changed,
            submitted: outcome.Submitted,
            cancelled: outcome.Cancelled,
            disabled: !enabled,
            readOnly: readOnly);
    }

    /// <summary>Draws a multiline editable text area.</summary>
    public Response TextArea(
        string id,
        ref string text,
        float width,
        float height,
        float? size = null,
        string? placeholder = null,
        bool enabled = true,
        bool readOnly = false)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = Theme.TextFieldPadding;
        int widgetId = MakeId(id);
        float border = FrameBorderWidth;

        string normalizedText = SanitizeTextInput(text, allowNewlines: true);
        if (normalizedText != text) text = normalizedText;

        var state = GetTextFieldState(widgetId);
        ClampTextFieldState(state, text);

        var metrics = MeasureTextArea(text, s);
        TextLayoutResult? placeholderLayout = null;

        float minHeight = metrics.LineHeight + pad.Vertical + border * 2;
        float h = MathF.Max(height, minHeight);
        float viewH = MathF.Max(0, h - border * 2 - pad.Vertical);
        float viewW = MathF.Max(0, width - border * 2 - pad.Horizontal);
        var (x, y) = Place(width, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        if ((!focused || readOnly) && state.Editing)
            EndEditSession(state);

        bool hover = enabled && PointIn(x, y, width, h);
        if (hover) _hotId = widgetId;
        if (enabled && (hover || focused)) RequestCursor(UiCursor.IBeam);

        float textX = x + border + pad.Left;
        float textY = y + border + pad.Top;
        bool mousePressed = enabled && hover && IsMousePressed(UiMouseButton.Left);
        bool wasFocused = focused;

        if (hover && _input.WheelDelta.Y != 0)
        {
            float maxScrollY = MathF.Max(0, metrics.ContentHeight - viewH);
            state.ScrollY = Math.Clamp(state.ScrollY - _input.WheelDelta.Y * Theme.ScrollWheelStep, 0, maxScrollY);
        }

        if (mousePressed)
        {
            SetFocus(widgetId);
            _activeId = widgetId;
            focused = true;
            if (!wasFocused && !readOnly)
                BeginEditSession(state, text);

            int hit = metrics.HitTest(
                _mouse.X - textX + state.ScrollX,
                _mouse.Y - textY + state.ScrollY);
            if (!(_input.Shift && wasFocused))
                state.SelectionAnchor = hit;

            state.CaretIndex = hit;
            state.HasPreferredCaretX = false;
        }
        else if (enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left))
        {
            SetFocus(widgetId);
            focused = true;
            state.CaretIndex = metrics.HitTest(
                _mouse.X - textX + state.ScrollX,
                _mouse.Y - textY + state.ScrollY);
            state.HasPreferredCaretX = false;
        }
        else if (focused && !state.Editing && !readOnly)
        {
            BeginEditSession(state, text);
        }

        TextInputOutcome outcome = default;
        if (enabled && focused)
        {
            if (readOnly)
            {
                ApplyReadOnlyTextAreaInput(state, text, metrics);
            }
            else
            {
                outcome = ApplyTextAreaInput(widgetId, state, ref text, metrics);
                if (outcome.Changed)
                    metrics = MeasureTextArea(text, s);

                if (outcome.Submitted || outcome.Cancelled)
                    focused = false;
            }
        }

        ClampTextFieldState(state, text);
        EnsureTextAreaCaretVisible(state, metrics, viewW, viewH);

        bool pressed = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        bool clicked = enabled && IsMouseReleased(UiMouseButton.Left) && _activeId == widgetId && hover;

        Color borderColor = !enabled ? Theme.TextFieldBorder.WithAlpha(140)
            : focused ? Theme.TextFieldBorderFocused
            : Theme.TextFieldBorder;
        Color bg = !enabled ? Theme.TextFieldBg.WithAlpha(180)
            : focused ? Theme.TextFieldBgFocused
            : hover ? Theme.TextFieldBgHover
            : Theme.TextFieldBg;

        DrawFrameRect(x, y, width, h, bg, borderColor);

        if (viewW > 0 && viewH > 0)
        {
            _painter.PushClip(textX, textY, viewW, viewH);

            if (HasSelection(state))
                DrawTextAreaSelection(state, metrics, textX, textY);

            if (text.Length == 0)
            {
                if (!string.IsNullOrEmpty(placeholder))
                {
                    placeholderLayout ??= LayoutText(placeholder, s);
                    DrawTextLayout(
                        placeholderLayout.Value,
                        textX,
                        textY,
                        enabled ? Theme.TextFieldPlaceholder : Theme.TextFieldPlaceholder.WithAlpha(140));
                }
            }
            else
            {
                foreach (var line in metrics.Lines)
                {
                    float lineY = textY + line.Y - state.ScrollY;
                    if (lineY + metrics.LineHeight < textY || lineY > textY + viewH)
                        continue;

                    DrawTextLayout(
                        line.Layout,
                        textX - state.ScrollX,
                        lineY,
                        enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140));
                }
            }

            if (enabled && focused && !readOnly)
            {
                Vector2 caret = metrics.GetCaretPosition(state.CaretIndex);
                _painter.DrawRect(
                    textX + caret.X - state.ScrollX,
                    textY + caret.Y - state.ScrollY,
                    1,
                    metrics.LineHeight,
                    Theme.TextFieldCaret);
            }

            _painter.PopClip();
        }

        Advance(width, h);
        return new Response(
            x,
            y,
            width,
            h,
            hover,
            pressed,
            clicked,
            focused: focused,
            changed: outcome.Changed,
            submitted: outcome.Submitted,
            cancelled: outcome.Cancelled,
            disabled: !enabled,
            readOnly: readOnly);
    }

    private TextAreaMetrics MeasureTextArea(string text, float size)
    {
        var atlas = GetAtlas(size);
        atlas.EnsureGlyphsForText(_renderer, text);

        var vm = atlas.GetScaledVMetrics();
        float lineHeight = MathF.Ceiling(vm.Ascent - vm.Descent);
        float lineAdvance = MathF.Ceiling(vm.Ascent - vm.Descent + vm.LineGap);
        int[] graphemeIndices = GetGraphemeIndices(text, out int graphemeIndexCount);

        var lines = new List<TextAreaLine>();
        int lineStartChar = 0;
        int lineStartGrapheme = 0;
        int graphemeCursor = 0;
        float maxWidth = 0;

        for (int i = 0; i <= text.Length; i++)
        {
            bool hasLineBreak = i < text.Length && text[i] == '\n';
            if (!hasLineBreak && i != text.Length)
                continue;

            string lineText = text.Substring(lineStartChar, i - lineStartChar);
            while (graphemeCursor < graphemeIndexCount - 1 && graphemeIndices[graphemeCursor] < i)
                graphemeCursor++;

            int graphemeCount = Math.Max(0, graphemeCursor - lineStartGrapheme);
            var metrics = TextLayout.MeasureSingleLine(_textScratch, lineText, atlas);
            var layout = TextLayout.Layout(_textScratch, lineText, atlas, null, TextWrapMode.NoWrap, TextOverflowMode.Visible, 1, GetEllipsisText());
            float lineY = lines.Count * lineAdvance;

            lines.Add(new TextAreaLine
            {
                Text = lineText,
                StartGrapheme = lineStartGrapheme,
                GraphemeCount = graphemeCount,
                HasLineBreak = hasLineBreak,
                Y = lineY,
                Metrics = metrics,
                Layout = layout
            });

            maxWidth = MathF.Max(maxWidth, metrics.Width);

            if (!hasLineBreak)
                break;

            lineStartChar = i + 1;
            lineStartGrapheme = Math.Min(graphemeCursor + 1, graphemeIndexCount - 1);
            graphemeCursor = lineStartGrapheme;
        }

        if (lines.Count == 0)
        {
            lines.Add(new TextAreaLine
            {
                Text = string.Empty,
                StartGrapheme = 0,
                GraphemeCount = 0,
                HasLineBreak = false,
                Y = 0,
                Metrics = TextLayout.MeasureSingleLine(_textScratch, string.Empty, atlas),
                Layout = TextLayout.Layout(_textScratch, string.Empty, atlas, null, TextWrapMode.NoWrap, TextOverflowMode.Visible, 1, GetEllipsisText())
            });
        }

        float contentHeight = lineHeight + MathF.Max(0, lines.Count - 1) * lineAdvance;
        return new TextAreaMetrics(lines.ToArray(), maxWidth, contentHeight, lineHeight, lineAdvance);
    }

    private TextFieldState GetTextFieldState(int id)
        => GetState<TextFieldState>(id);

    private static void BeginEditSession(TextFieldState state, string text)
    {
        state.Editing = true;
        state.EditStartText = text;
        state.HasPreferredCaretX = false;
    }

    private static void EndEditSession(TextFieldState state)
    {
        state.Editing = false;
        state.EditStartText = string.Empty;
        state.HasPreferredCaretX = false;
    }

    private void ApplyReadOnlyTextFieldInput(TextFieldState state, string text)
    {
        bool shortcut = _input.PrimaryModifier;

        if (shortcut && _input.IsPressed(UiKey.A))
        {
            int graphemeCount = GetGraphemeCount(text);
            state.SelectionAnchor = 0;
            state.CaretIndex = graphemeCount;
        }

        if (shortcut && _input.IsPressed(UiKey.C))
            CopySelectionToClipboard(text, state);

        if (_input.IsPressed(UiKey.Left))
        {
            if (!_input.Shift && HasSelection(state))
                CollapseSelection(state, GetSelectionRange(state).start);
            else
                MoveCaret(state, Math.Max(0, state.CaretIndex - 1), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Right))
        {
            if (!_input.Shift && HasSelection(state))
                CollapseSelection(state, GetSelectionRange(state).end);
            else
                MoveCaret(state, Math.Min(GetGraphemeCount(text), state.CaretIndex + 1), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Home))
            MoveCaret(state, 0, _input.Shift);

        if (_input.IsPressed(UiKey.End))
            MoveCaret(state, GetGraphemeCount(text), _input.Shift);
    }

    private void ApplyReadOnlyTextAreaInput(TextFieldState state, string text, TextAreaMetrics metrics)
    {
        bool shortcut = _input.PrimaryModifier;

        if (shortcut && _input.IsPressed(UiKey.A))
        {
            int graphemeCount = GetGraphemeCount(text);
            state.SelectionAnchor = 0;
            state.CaretIndex = graphemeCount;
            state.HasPreferredCaretX = false;
        }

        if (shortcut && _input.IsPressed(UiKey.C))
            CopySelectionToClipboard(text, state);

        if (_input.IsPressed(UiKey.Left))
        {
            if (!_input.Shift && HasSelection(state))
                CollapseSelection(state, GetSelectionRange(state).start);
            else
                MoveCaret(state, Math.Max(0, state.CaretIndex - 1), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Right))
        {
            if (!_input.Shift && HasSelection(state))
                CollapseSelection(state, GetSelectionRange(state).end);
            else
                MoveCaret(state, Math.Min(GetGraphemeCount(text), state.CaretIndex + 1), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Up))
            MoveCaretVertical(state, metrics, -1, _input.Shift);

        if (_input.IsPressed(UiKey.Down))
            MoveCaretVertical(state, metrics, 1, _input.Shift);

        if (_input.IsPressed(UiKey.Home))
        {
            if (shortcut)
                MoveCaret(state, 0, _input.Shift);
            else
                MoveCaret(state, metrics.GetLineStart(state.CaretIndex), _input.Shift);
        }

        if (_input.IsPressed(UiKey.End))
        {
            if (shortcut)
                MoveCaret(state, GetGraphemeCount(text), _input.Shift);
            else
                MoveCaret(state, metrics.GetLineEnd(state.CaretIndex), _input.Shift);
        }
    }

    private TextInputOutcome ApplyTextFieldInput(int widgetId, TextFieldState state, ref string text)
    {
        bool changed = false;
        bool submitted = false;
        bool cancelled = false;
        bool shortcut = _input.PrimaryModifier;

        if (shortcut && _input.IsPressed(UiKey.A))
        {
            int graphemeCount = GetGraphemeCount(text);
            state.SelectionAnchor = 0;
            state.CaretIndex = graphemeCount;
        }

        if (shortcut && _input.IsPressed(UiKey.C))
            CopySelectionToClipboard(text, state);

        if (shortcut && _input.IsPressed(UiKey.X))
        {
            CopySelectionToClipboard(text, state);
            changed |= DeleteSelection(ref text, state);
        }

        if (shortcut && _input.IsPressed(UiKey.V))
            changed |= PasteClipboard(ref text, state);

        if (_input.IsPressed(UiKey.Left))
        {
            if (!_input.Shift && HasSelection(state))
                CollapseSelection(state, GetSelectionRange(state).start);
            else
                MoveCaret(state, Math.Max(0, state.CaretIndex - 1), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Right))
        {
            if (!_input.Shift && HasSelection(state))
                CollapseSelection(state, GetSelectionRange(state).end);
            else
                MoveCaret(state, Math.Min(GetGraphemeCount(text), state.CaretIndex + 1), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Home))
            MoveCaret(state, 0, _input.Shift);

        if (_input.IsPressed(UiKey.End))
            MoveCaret(state, GetGraphemeCount(text), _input.Shift);

        if (_input.IsPressed(UiKey.Backspace))
            changed |= DeleteBackward(ref text, state);

        if (_input.IsPressed(UiKey.Delete))
            changed |= DeleteForward(ref text, state);

        string insertedText = SanitizeTextInput(_input.TextInput);
        if (insertedText.Length > 0)
            changed |= ReplaceSelection(ref text, insertedText, state);

        if (_input.IsPressed(UiKey.Enter))
        {
            submitted = true;
            ClearFocus(widgetId);
            EndEditSession(state);
        }
        else if (_input.IsPressed(UiKey.Escape))
        {
            if (state.Editing && text != state.EditStartText)
            {
                text = state.EditStartText;
                changed = true;
            }

            cancelled = true;
            ClearFocus(widgetId);
            EndEditSession(state);
        }

        return new TextInputOutcome(changed, submitted, cancelled);
    }

    private TextInputOutcome ApplyTextAreaInput(int widgetId, TextFieldState state, ref string text, TextAreaMetrics metrics)
    {
        bool changed = false;
        bool submitted = false;
        bool cancelled = false;
        bool shortcut = _input.PrimaryModifier;

        if (shortcut && _input.IsPressed(UiKey.A))
        {
            int graphemeCount = GetGraphemeCount(text);
            state.SelectionAnchor = 0;
            state.CaretIndex = graphemeCount;
            state.HasPreferredCaretX = false;
        }

        if (shortcut && _input.IsPressed(UiKey.C))
            CopySelectionToClipboard(text, state);

        if (shortcut && _input.IsPressed(UiKey.X))
        {
            CopySelectionToClipboard(text, state);
            changed |= DeleteSelection(ref text, state);
        }

        if (shortcut && _input.IsPressed(UiKey.V))
            changed |= PasteClipboard(ref text, state, allowNewlines: true);

        if (_input.IsPressed(UiKey.Left))
        {
            if (!_input.Shift && HasSelection(state))
                CollapseSelection(state, GetSelectionRange(state).start);
            else
                MoveCaret(state, Math.Max(0, state.CaretIndex - 1), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Right))
        {
            if (!_input.Shift && HasSelection(state))
                CollapseSelection(state, GetSelectionRange(state).end);
            else
                MoveCaret(state, Math.Min(GetGraphemeCount(text), state.CaretIndex + 1), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Up))
            MoveCaretVertical(state, metrics, -1, _input.Shift);

        if (_input.IsPressed(UiKey.Down))
            MoveCaretVertical(state, metrics, 1, _input.Shift);

        if (_input.IsPressed(UiKey.Home))
        {
            if (shortcut)
                MoveCaret(state, 0, _input.Shift);
            else
                MoveCaret(state, metrics.GetLineStart(state.CaretIndex), _input.Shift);
        }

        if (_input.IsPressed(UiKey.End))
        {
            if (shortcut)
                MoveCaret(state, GetGraphemeCount(text), _input.Shift);
            else
                MoveCaret(state, metrics.GetLineEnd(state.CaretIndex), _input.Shift);
        }

        if (_input.IsPressed(UiKey.Backspace))
            changed |= DeleteBackward(ref text, state);

        if (_input.IsPressed(UiKey.Delete))
            changed |= DeleteForward(ref text, state);

        string insertedText = SanitizeTextInput(_input.TextInput, allowNewlines: true);
        if (insertedText.Length > 0)
            changed |= ReplaceSelection(ref text, insertedText, state);

        if (_input.IsPressed(UiKey.Enter))
        {
            if (shortcut)
            {
                submitted = true;
                ClearFocus(widgetId);
                EndEditSession(state);
            }
            else
            {
                changed |= ReplaceSelection(ref text, "\n", state);
            }
        }
        else if (_input.IsPressed(UiKey.Escape))
        {
            if (state.Editing && text != state.EditStartText)
            {
                text = state.EditStartText;
                changed = true;
            }

            cancelled = true;
            ClearFocus(widgetId);
            EndEditSession(state);
        }

        return new TextInputOutcome(changed, submitted, cancelled);
    }

    private static void MoveCaret(TextFieldState state, int newIndex, bool extendSelection, bool clearPreferredCaretX = true)
    {
        state.CaretIndex = newIndex;
        if (!extendSelection) state.SelectionAnchor = newIndex;
        if (clearPreferredCaretX) state.HasPreferredCaretX = false;
    }

    private static void CollapseSelection(TextFieldState state, int index)
    {
        state.SelectionAnchor = index;
        state.CaretIndex = index;
        state.HasPreferredCaretX = false;
    }

    private static void MoveCaretVertical(TextFieldState state, TextAreaMetrics metrics, int deltaLines, bool extendSelection)
    {
        float desiredX = state.HasPreferredCaretX
            ? state.PreferredCaretX
            : metrics.GetCaretPosition(state.CaretIndex).X;

        state.CaretIndex = metrics.MoveVertical(state.CaretIndex, desiredX, deltaLines);
        if (!extendSelection)
            state.SelectionAnchor = state.CaretIndex;

        state.PreferredCaretX = desiredX;
        state.HasPreferredCaretX = true;
    }

    private bool DeleteBackward(ref string text, TextFieldState state)
    {
        if (DeleteSelection(ref text, state)) return true;
        if (state.CaretIndex <= 0) return false;

        int end = state.CaretIndex;
        return RemoveGraphemeRange(ref text, state, end - 1, end);
    }

    private bool DeleteForward(ref string text, TextFieldState state)
    {
        if (DeleteSelection(ref text, state)) return true;

        int graphemeCount = GetGraphemeCount(text);
        if (state.CaretIndex >= graphemeCount) return false;

        int start = state.CaretIndex;
        return RemoveGraphemeRange(ref text, state, start, start + 1);
    }

    private bool DeleteSelection(ref string text, TextFieldState state)
    {
        if (!HasSelection(state)) return false;
        var (start, end) = GetSelectionRange(state);
        return RemoveGraphemeRange(ref text, state, start, end);
    }

    private bool RemoveGraphemeRange(ref string text, TextFieldState state, int start, int end)
    {
        int[] indices = GetGraphemeIndices(text, out int count);
        if (start < 0 || end <= start || end >= count) return false;

        int startChar = indices[start];
        int endChar = indices[end];
        text = text.Remove(startChar, endChar - startChar);
        CollapseSelection(state, start);
        return true;
    }

    private bool ReplaceSelection(ref string text, string replacement, TextFieldState state)
    {
        int[] indices = GetGraphemeIndices(text, out int _);
        var (start, end) = GetSelectionRange(state);
        int startChar = indices[start];
        int endChar = indices[end];

        text = text.Remove(startChar, endChar - startChar).Insert(startChar, replacement);
        int insertedGraphemes = GetGraphemeCount(replacement);
        CollapseSelection(state, start + insertedGraphemes);
        return true;
    }

    private void CopySelectionToClipboard(string text, TextFieldState state)
    {
        if (!HasSelection(state)) return;
        Platform.SetClipboardText(GetSelectedText(text, state));
    }

    private bool PasteClipboard(ref string text, TextFieldState state, bool allowNewlines = false)
    {
        string clipboardText = SanitizeTextInput(Platform.GetClipboardText(), allowNewlines);
        if (clipboardText.Length == 0) return false;
        return ReplaceSelection(ref text, clipboardText, state);
    }

    private string GetSelectedText(string text, TextFieldState state)
    {
        int[] indices = GetGraphemeIndices(text, out int _);
        var (start, end) = GetSelectionRange(state);
        int startChar = indices[start];
        int endChar = indices[end];
        return text.Substring(startChar, endChar - startChar);
    }

    private static void ClampTextFieldState(TextFieldState state, string text)
    {
        int graphemeCount = GetGraphemeCount(text);
        state.CaretIndex = Math.Clamp(state.CaretIndex, 0, graphemeCount);
        state.SelectionAnchor = Math.Clamp(state.SelectionAnchor, 0, graphemeCount);
        if (state.ScrollX < 0) state.ScrollX = 0;
        if (state.ScrollY < 0) state.ScrollY = 0;
    }

    private static void EnsureCaretVisible(TextFieldState state, TextLineMetrics metrics, float visibleWidth)
    {
        visibleWidth = MathF.Max(0, visibleWidth);
        float caretX = metrics.GetCaretX(state.CaretIndex);

        if (caretX < state.ScrollX)
            state.ScrollX = caretX;
        else if (caretX > state.ScrollX + visibleWidth)
            state.ScrollX = caretX - visibleWidth;

        float maxScroll = MathF.Max(0, metrics.Width - visibleWidth);
        state.ScrollX = Math.Clamp(state.ScrollX, 0, maxScroll);
    }

    private static void EnsureTextAreaCaretVisible(TextFieldState state, TextAreaMetrics metrics, float visibleWidth, float visibleHeight)
    {
        visibleWidth = MathF.Max(0, visibleWidth);
        visibleHeight = MathF.Max(0, visibleHeight);
        Vector2 caret = metrics.GetCaretPosition(state.CaretIndex);

        if (caret.X < state.ScrollX)
            state.ScrollX = caret.X;
        else if (caret.X > state.ScrollX + visibleWidth)
            state.ScrollX = caret.X - visibleWidth;

        if (caret.Y < state.ScrollY)
            state.ScrollY = caret.Y;
        else if (caret.Y + metrics.LineHeight > state.ScrollY + visibleHeight)
            state.ScrollY = caret.Y + metrics.LineHeight - visibleHeight;

        float maxScrollX = MathF.Max(0, metrics.ContentWidth - visibleWidth);
        float maxScrollY = MathF.Max(0, metrics.ContentHeight - visibleHeight);
        state.ScrollX = Math.Clamp(state.ScrollX, 0, maxScrollX);
        state.ScrollY = Math.Clamp(state.ScrollY, 0, maxScrollY);
    }

    private void DrawTextAreaSelection(TextFieldState state, TextAreaMetrics metrics, float textX, float textY)
    {
        var (selectionStart, selectionEnd) = GetSelectionRange(state);
        foreach (var line in metrics.Lines)
        {
            if (selectionEnd <= line.StartGrapheme || selectionStart >= line.NextLineStartGrapheme)
                continue;

            int localStart = Math.Clamp(selectionStart - line.StartGrapheme, 0, line.GraphemeCount);
            int localEnd = selectionEnd >= line.NextLineStartGrapheme
                ? line.GraphemeCount
                : Math.Clamp(selectionEnd - line.StartGrapheme, 0, line.GraphemeCount);

            if (localEnd <= localStart)
                continue;

            float selectionX = textX + line.Metrics.GetCaretX(localStart) - state.ScrollX;
            float selectionY = textY + line.Y - state.ScrollY;
            float selectionW = line.Metrics.GetCaretX(localEnd) - line.Metrics.GetCaretX(localStart);
            if (selectionW > 0)
                _painter.DrawRect(selectionX, selectionY, selectionW, metrics.LineHeight, Theme.TextFieldSelectionBg);
        }
    }

    private static bool HasSelection(TextFieldState state) => state.CaretIndex != state.SelectionAnchor;

    private static (int start, int end) GetSelectionRange(TextFieldState state)
    {
        int start = Math.Min(state.CaretIndex, state.SelectionAnchor);
        int end = Math.Max(state.CaretIndex, state.SelectionAnchor);
        return (start, end);
    }

    private static int GetGraphemeCount(string text)
    {
        int count = 0;
        var span = text.AsSpan();
        int charIndex = 0;
        while (charIndex < text.Length)
        {
            charIndex += MeasureGraphemeCluster(span, charIndex);
            count++;
        }
        return count;
    }

    private int[] GetGraphemeIndices(string text, out int count)
    {
        int written = 0;
        var span = text.AsSpan();
        int charIndex = 0;
        while (charIndex < text.Length)
        {
            if (written + 1 >= _graphemeIndexScratch.Length)
                Array.Resize(ref _graphemeIndexScratch, _graphemeIndexScratch.Length * 2);
            _graphemeIndexScratch[written++] = charIndex;
            charIndex += MeasureGraphemeCluster(span, charIndex);
        }
        if (written >= _graphemeIndexScratch.Length)
            Array.Resize(ref _graphemeIndexScratch, _graphemeIndexScratch.Length * 2);
        _graphemeIndexScratch[written] = text.Length;
        count = written + 1;
        return _graphemeIndexScratch;
    }

    private static int MeasureGraphemeCluster(ReadOnlySpan<char> text, int start)
    {
        var status = Rune.DecodeFromUtf16(text.Slice(start), out _, out int consumed);
        if (status != System.Buffers.OperationStatus.Done) return 1;

        int cursor = start + consumed;
        while (cursor < text.Length)
        {
            var s = Rune.DecodeFromUtf16(text.Slice(cursor), out Rune next, out int nextConsumed);
            if (s != System.Buffers.OperationStatus.Done) break;
            var category = Rune.GetUnicodeCategory(next);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark &&
                category != System.Globalization.UnicodeCategory.SpacingCombiningMark &&
                category != System.Globalization.UnicodeCategory.EnclosingMark)
                break;
            cursor += nextConsumed;
        }

        return cursor - start;
    }

    private static string SanitizeTextInput(string text, bool allowNewlines = false)
    {
        if (text.Length == 0) return text;

        if (allowNewlines)
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        StringBuilder? builder = null;
        int charIndex = 0;

        foreach (Rune rune in text.EnumerateRunes())
        {
            bool keep = allowNewlines
                ? !Rune.IsControl(rune) || rune.Value == '\n'
                : !Rune.IsControl(rune) && rune.Value != '\r' && rune.Value != '\n';
            if (!keep)
            {
                builder ??= new StringBuilder(text.Length);
                if (builder.Length == 0 && charIndex > 0)
                    builder.Append(text, 0, charIndex);
            }
            else if (builder != null)
            {
                builder.Append(rune.ToString());
            }

            charIndex += rune.Utf16SequenceLength;
        }

        return builder?.ToString() ?? text;
    }
}
