using SoZvon.UI.SubClasses;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SoZvon.UI.My_Controls
{
    public enum My_TextBox_Type
    {
        Send_RichTextBox = 0,
        Messages_Form = 1,
    }
    public partial class My_TextBox : UserControl
    {
        const int maxWidth_txt = 414;

        public My_TextBox()
        {
            InitializeComponent();
            txtTextbox.AcceptsReturn = false;

            DataObject.AddCopyingHandler(txtTextbox, OnClipboardCopying);
            DataObject.AddPastingHandler(txtTextbox, myRichTextBox_Pasting);
        }
        public My_TextBox(My_TextBox_Type type)
        {
            InitializeComponent();
            txtTextbox.AcceptsReturn = false;

            if (type is My_TextBox_Type.Messages_Form)
            {
                txtTextbox.MaxWidth = maxWidth_txt;
                txtTextbox.Document.DataContextChanged += (s, e) => UpdateRichTextBoxWidth();
            }

            DataObject.AddCopyingHandler(txtTextbox, OnClipboardCopying);
            DataObject.AddPastingHandler(txtTextbox, myRichTextBox_Pasting);
        }


        public event EventHandler<TextChangedEventArgs> TextChanged;


        bool m_IgnoreChanges = false;
        public bool AcceptsReturn
        {
            get { return (bool)GetValue(AcceptsReturnProperty); }
            set { SetValue(AcceptsReturnProperty, value); }
        }
        public bool AcceptsTab
        {
            get { return (bool)GetValue(AcceptsTabProperty); }
            set { SetValue(AcceptsTabProperty, value); }
        }
        public string Text
        {
            get { return GetPlainText(); }
            set { SetText(value); }
        }
        public bool IsReadOnly
        {
            get { return txtTextbox.IsReadOnly; }
            set { txtTextbox.IsReadOnly = value; }
        }
        public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;


        public static readonly DependencyProperty AcceptsReturnProperty = DependencyProperty.Register("AcceptsReturn", typeof(bool), typeof(My_TextBox), new UIPropertyMetadata(false, OnAcceptsReturnChanged));
        static void OnAcceptsReturnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is My_TextBox textBox) textBox.txtTextbox.AcceptsReturn = (bool)e.NewValue;
        }

        public static readonly DependencyProperty AcceptsTabProperty = DependencyProperty.Register("AcceptsTab", typeof(bool), typeof(My_TextBox), new UIPropertyMetadata(false, OnAcceptsTabChanged));
        static void OnAcceptsTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is My_TextBox textBox) textBox.txtTextbox.AcceptsTab = (bool)e.NewValue;
        }


        void txtTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!m_IgnoreChanges)
            {
                m_IgnoreChanges = true;
                EmoticonsHelper.ParseText(txtTextbox);
                m_IgnoreChanges = false;

                ChangeTextAlignment(TextAlignment);

                TextChanged?.Invoke(this, e);
            }
        }
        public void UpdateRichTextBoxWidth()
        {
            // Создаем временный TextBlock для измерения
            var measureBlock = new TextBlock
            {
                FontFamily = txtTextbox.FontFamily,
                FontSize = txtTextbox.FontSize,
                FontWeight = txtTextbox.FontWeight,
                FontStyle = txtTextbox.FontStyle,
                TextWrapping = TextWrapping.NoWrap
            };

            // Преобразуем содержимое RichTextBox в текст с учетом изображений
            var text = ExtractContentWithImagePlaceholders(txtTextbox.Document);
            measureBlock.Text = text;

            // Измеряем
            measureBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            txtTextbox.MaxWidth = measureBlock.DesiredSize.Width + txtTextbox.Padding.Left + txtTextbox.Padding.Right + 15;
        }
        static string ExtractContentWithImagePlaceholders(FlowDocument document)
        {
            StringBuilder sb = new();

            foreach (Block block in document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (Inline inline in paragraph.Inlines)
                    {
                        if (inline is Run run)
                        {
                            sb.Append(run.Text);
                        }
                        else if (inline is InlineUIContainer uiContainer && uiContainer.Child is Image image)
                        {
                            // Добавляем пробелы в качестве плейсхолдера для изображения
                            if (image.Width == 30) sb.Append("       ");
                        }
                    }
                }
            }

            return sb.ToString();
        }


        void OnClipboardCopying(object sender, DataObjectCopyingEventArgs e)
        {
            e.Handled = true;
            e.CancelCommand();

            // Получаем текст с заменой изображений
            string processedText = GetTextWithReplacedImages(txtTextbox.Selection);

            DataObject newData = new();
            newData.SetText(processedText);
            Clipboard.SetDataObject(newData);
        }
        string GetTextWithReplacedImages(TextSelection selection)
        {
            StringBuilder result = new();
            TextPointer current = selection.Start;
            TextPointer end = selection.End;

            int selectionStartOffset = selection.Start.FindPosition(txtTextbox.Document.ContentStart);
            int selectionEndOffset = selection.End.FindPosition(txtTextbox.Document.ContentStart);

            while (current != null && current.CompareTo(end) < 0)
            {
                TextPointerContext context = current.GetPointerContext(LogicalDirection.Forward);

                if (context is TextPointerContext.ElementStart or TextPointerContext.ElementEnd)
                {
                    current = current.GetNextContextPosition(LogicalDirection.Forward);
                }
                else if (context == TextPointerContext.Text)
                {
                    string textInRun = current.GetTextInRun(LogicalDirection.Forward);
                    int charsRemainingInSelection = current.FindPosition(end);
                    int charsToAppend = Math.Min(textInRun.Length, charsRemainingInSelection);

                    if (charsToAppend > 0)
                    {
                        string appendedText = textInRun[..charsToAppend];
                        result.Append(appendedText);
                    }

                    current = current.GetPositionAtOffset(charsToAppend, LogicalDirection.Forward);
                }
                else if (context == TextPointerContext.None || context == TextPointerContext.EmbeddedElement)
                {
                    DependencyObject element = current.Parent;

                    if (element is InlineUIContainer container)
                    {
                        int containerStartOffset = container.ElementStart.FindPosition(txtTextbox.Document.ContentStart);
                        int containerEndOffset = container.ElementEnd.FindPosition(txtTextbox.Document.ContentStart);

                        bool isContainerFullySelected = containerStartOffset >= selectionStartOffset && containerEndOffset <= selectionEndOffset;

                        if (isContainerFullySelected)
                        {
                            if (container.Child is Image image)
                            {
                                string imageTag = image.Tag?.ToString() ?? "[error img]";
                                result.Append(imageTag);
                                //Debug.WriteLine($"{indent}{indent}  -> Добавлено изображение: \"{imageTag}\"");
                            }
                        }
                        
                        current = container.ElementEnd;
                    }
                    else
                    {
                        current = current.GetNextContextPosition(LogicalDirection.Forward);
                    }
                }

                // Защита от бесконечного цикла или выхода за пределы
                if (current == null)
                {
                    //Debug.WriteLine($"Указатель стал null. Прекращаем обход.");
                    break;
                }
                if (current.CompareTo(end) > 0)
                {
                    //Debug.WriteLine($"Указатель (offset {current.FindPosition(txtTextbox.Document.ContentStart)}) вышел за конец выделения (offset {selectionEndOffset}). Прекращаем обход.");
                    break;
                }
            }

            return result.ToString();
        }
        static string GetTextFromSpan(Span span)
        {
            StringBuilder sb = new();

            foreach (var inline in span.Inlines)
            {
                if (inline is Run run)
                {
                    sb.Append(run.Text);
                }
                else if (inline is InlineUIContainer container && container.Child is Image image)
                {
                    sb.Append(image.Tag?.ToString() ?? "[неизвестное изображение]");
                }
                else if (inline is Span nestedSpan)
                {
                    sb.Append(GetTextFromSpan(nestedSpan));
                }
            }

            return sb.ToString();
        }


        void myRichTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
            e.Handled = true;

            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
            {
                string? textToPaste = e.DataObject.GetData(DataFormats.UnicodeText) as string;

                if (!string.IsNullOrEmpty(textToPaste))
                {
                    double fontSize = txtTextbox.FontSize;
                    FontFamily fontFamily = txtTextbox.FontFamily;
                    FontWeight fontWeight = txtTextbox.FontWeight;
                    FontStyle fontStyle = FontStyles.Normal;

                    InsertFormattedText(txtTextbox, textToPaste, fontSize, fontFamily, fontWeight, fontStyle);
                }
            }
        }
        static void InsertFormattedText(RichTextBox richTextBox, string text, double fontSize, FontFamily fontFamily, FontWeight fontWeight = default, FontStyle fontStyle = default)
        {
            if (fontWeight == default) fontWeight = FontWeights.Normal;
            if (fontStyle == default) fontStyle = FontStyles.Normal;

            Run newRun = new(text)
            {
                FontSize = fontSize,
                FontFamily = fontFamily,
                FontWeight = fontWeight,
                FontStyle = fontStyle
            };

            Paragraph currentParagraph = richTextBox.CaretPosition.Paragraph ?? new Paragraph();

            currentParagraph.Inlines.Add(newRun);

            if (richTextBox.CaretPosition.Paragraph is null)
                richTextBox.Document.Blocks.Add(currentParagraph);

            richTextBox.CaretPosition = newRun.ElementEnd;
        }


        void UserControl_GotFocus(object sender, RoutedEventArgs e) => Keyboard.Focus(txtTextbox);
        void ChangeTextAlignment(TextAlignment textAlignment)
        {
            txtTextbox.Document ??= new FlowDocument();
            txtTextbox.Document.TextAlignment = textAlignment;

            foreach (Block block in txtTextbox.Document.Blocks)
            {
                if (block is Paragraph paragraph && paragraph.TextAlignment != textAlignment)
                    paragraph.TextAlignment = textAlignment;
            }
        }
        public string GetPlainText() => EmoticonsHelper.GetPlainText(txtTextbox.Document);
        void SetText(string text)
        {
            FlowDocument flowDoc = new();
            Paragraph paragraph = new();

            paragraph.Inlines.Add(new Run(text));
            flowDoc.Blocks.Add(paragraph);

            txtTextbox.Document = flowDoc;
        }
        public void Clear() => txtTextbox.Document = new();
    }


    public static class TextPointerExtensions
    {
        public static int FindPosition(this TextPointer pointer, TextPointer textPointer) => Math.Abs(pointer.GetOffsetToPosition(textPointer));
    }
}
