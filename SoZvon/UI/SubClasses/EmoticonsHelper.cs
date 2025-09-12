using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SoZvon.UI.SubClasses
{
    class EmoticonsHelper
    {
        static Dictionary<string, string> m_Emoticons;
        static Dictionary<string, string> m_EmoticonsReverse;

        static void InitEmoticons()
        {
            m_Emoticons = new Dictionary<string, string>
            {
                ["^_^"] = "images/emoticons/01.png",
                [":D"] = "images/emoticons/02.png",
                [";)"] = "images/emoticons/04.png",
                [":)"] = "images/emoticons/05.png",
                ["8)"] = "images/emoticons/07.png",
                [":p"] = "images/emoticons/08.png",
                [":o"] = "images/emoticons/10.png",
                [":("] = "images/emoticons/12.png",
                [":'("] = "images/emoticons/13.png",
                [":@"] = "images/emoticons/14.png",
                [">:@"] = "images/emoticons/14.png",
                [">_<"] = "images/emoticons/14.png",
                ["-_-"] = "images/emoticons/15.png",
                ["Х"] = "images/emoticons/16.png",
                ["У"] = "images/emoticons/17.png",
                ["Й"] = "images/emoticons/18.png"
            };

            m_EmoticonsReverse = [];

            foreach (string k in m_Emoticons.Keys) m_EmoticonsReverse[m_Emoticons[k]] = k;
        } // InitEmoticons
        static int FindFirstEmoticon(string text, int startIndex, out string emoticonFound)
        {
            InitEmoticons();
            emoticonFound = string.Empty;
            int minIndex = -1;

            if (m_Emoticons is null) return -1;

            foreach (string e in m_Emoticons.Keys)
            {
                int index = text.IndexOf(e, startIndex);

                if (index >= 0)
                {
                    if (minIndex < 0 || index < minIndex)
                    {
                        minIndex = index;
                        emoticonFound = e;
                    }
                }
            }
            return minIndex;
        }

        public static string GetPlainText(FlowDocument doc)
        {
            if (m_Emoticons is null) InitEmoticons();

            StringBuilder result = new();

            foreach (Block b in doc.Blocks.ToList())
            {
                if (b is Paragraph paragraph)
                {
                    foreach (Inline inline in paragraph.Inlines.ToList())
                    {
                        if (inline is Run run)
                        {
                            result.Append(run.Text);
                        }
                        else if (inline is InlineUIContainer inline_container)
                        {
                            if (inline_container.Child is Image image_ && image_.Source is BitmapImage img)
                            {
                                if (m_EmoticonsReverse.TryGetValue(img.UriSource.ToString(), out string? value)) result.Append(value);
                                else result.Append("[error img]");
                            }
                        }
                        else if (inline is Span span)
                        {
                            // Создаем Run с текстом из Span
                            result.Append(new Run(GetTextFromSpan(span)).Text);
                        }
                    }
                }
            }

            return result.ToString();
        }
        static string GetTextFromSpan(Span span)
        {
            StringBuilder sb = new();

            foreach (Inline inline in span.Inlines)
            {
                if (inline is Run run)
                    sb.Append(run.Text);
                else if (inline is Span nestedSpan)
                    sb.Append(GetTextFromSpan(nestedSpan));
            }
            return sb.ToString();
        }
        public static void ParseText(FrameworkElement element)
        {
            InitEmoticons();
            TextBlock? textBlock = null;
            RichTextBox? textBox = element as RichTextBox;

            if (textBox is null) textBlock = element as TextBlock;

            if (textBox is null && textBlock is null) return;

            if (textBox is not null)
            {
                FlowDocument doc = textBox.Document;

                for (int blockIndex = 0; blockIndex < doc.Blocks.Count; blockIndex++)
                {
                    Block b = doc.Blocks.ElementAt(blockIndex);

                    if (b is Paragraph p) 
                        ProcessInlines(textBox, p.Inlines);
                }
            }
            else if(textBlock is not null)
            {
                ProcessInlines(null, textBlock.Inlines);
            }
        }
        static void ProcessInlines(RichTextBox? textBox, InlineCollection inlines)
        {
            if (m_Emoticons is null) return;

            for (int inlineIndex = 0; inlineIndex < inlines.Count; inlineIndex++)
            {
                Inline i = inlines.ElementAt(inlineIndex);

                if (i is Run)
                {
                    if(i is not Run run) continue;

                    int index = FindFirstEmoticon(run.Text, 0, out string emoticonFound);

                    if (index != -1)
                    {
                        TextPointer tp = i.ContentStart;
                        bool reposition = false;

                        while (!tp.GetTextInRun(LogicalDirection.Forward).StartsWith(emoticonFound))
                            tp = tp.GetNextInsertionPosition(LogicalDirection.Forward);

                        TextPointer end = tp;
                        for (int j = 0; j < emoticonFound.Length; j++)
                            end = end.GetNextInsertionPosition(LogicalDirection.Forward);

                        TextRange tr = new(tp, end);

                        if (textBox != null) 
                            reposition = textBox.CaretPosition.CompareTo(tr.End) == 0;

                        tr.Text = string.Empty;

                        string imageFile = m_Emoticons[emoticonFound];

                        BitmapImage bimg = new();
                        bimg.BeginInit();
                        bimg.CacheOption = BitmapCacheOption.OnLoad;
                        bimg.UriSource = new Uri(imageFile, UriKind.RelativeOrAbsolute);
                        bimg.EndInit();

                        Image image = new();
                        image.Source = bimg;
                        image.Width = 30;
                        
                        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                        RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);

                        image.Tag = emoticonFound;

                        _ = new InlineUIContainer(image, tp) { BaselineAlignment = BaselineAlignment.TextBottom };

                        if (textBox != null && reposition)
                            textBox.CaretPosition = tp.GetNextInsertionPosition(LogicalDirection.Forward);
                    }
                }
            }
        }
    }
}
