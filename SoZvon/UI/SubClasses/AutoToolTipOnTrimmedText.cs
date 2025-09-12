using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SoZvon.UI.SubClasses
{
    public static class TextBlockUtils
    {
        const int offset_X = 10;
        const int offset_Y = 20;

        public static readonly DependencyProperty AutoToolTipOnTrimmedTextProperty = DependencyProperty.RegisterAttached("AutoToolTipOnTrimmedText", typeof(bool), typeof(TextBlockUtils), new PropertyMetadata(false, OnAutoToolTipOnTrimmedTextChanged));

        public static bool GetAutoToolTipOnTrimmedText(TextBlock obj) => (bool)obj.GetValue(AutoToolTipOnTrimmedTextProperty);
        public static void SetAutoToolTipOnTrimmedText(TextBlock obj, bool value) => obj.SetValue(AutoToolTipOnTrimmedTextProperty, value);
        private static void OnAutoToolTipOnTrimmedTextChanged(DependencyObject dep, DependencyPropertyChangedEventArgs e)
        {
            if (dep is not TextBlock textBlock) return;

            if ((bool)e.NewValue)
            {
                textBlock.MouseEnter += TextBlock_MouseEnter;
                textBlock.MouseLeave += TextBlock_MouseLeave;
                textBlock.SizeChanged += TextBlock_SizeChanged;
                textBlock.MouseMove += TextBlock_MouseMove;

                ToolTipService.SetInitialShowDelay(textBlock, 50);
                ToolTipService.SetShowDuration(textBlock, 100_000);
            }
            else
            {
                textBlock.MouseEnter -= TextBlock_MouseEnter;
                textBlock.MouseLeave -= TextBlock_MouseLeave;
                textBlock.SizeChanged -= TextBlock_SizeChanged;
                textBlock.MouseMove -= TextBlock_MouseMove;
                textBlock.ToolTip = null;
            }
        }


        // Всякие события ToolTip
        private static void TextBlock_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                if (textBlock.ToolTip is ToolTip toolTipInstance && toolTipInstance.IsOpen)
                {
                    Point mousePosition = e.GetPosition(textBlock);

                    // СМЕЩЕНИЕ ToolTip
                    toolTipInstance.HorizontalOffset = mousePosition.X + offset_X;
                    toolTipInstance.VerticalOffset = mousePosition.Y + offset_Y;
                }
            }
        }
        private static void TextBlock_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is TextBlock textBlock) UpdateToolTip(textBlock);
        }
        private static void TextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                if (textBlock.ToolTip is ToolTip toolTipInstance && toolTipInstance.IsOpen) toolTipInstance.IsOpen = false;

                textBlock.ToolTip = null;
            }
        }
        private static void TextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                if (textBlock.IsMouseOver)
                {
                    UpdateToolTip(textBlock, Mouse.GetPosition(textBlock));
                }
                else
                {
                    // Также сбросим, если текст стал необрезанным
                    if (!IsTextActuallyTrimmed(textBlock) && textBlock.ToolTip is ToolTip toolTipInstance && toolTipInstance.IsOpen)
                    {
                        toolTipInstance.IsOpen = false;
                        textBlock.ToolTip = null;
                    }
                }
            }
        }


        // Общая логика обновления ToolTip
        private static void UpdateToolTip(TextBlock textBlock, Point? mousePosition = null)
        {
            bool isTrimmed = IsTextActuallyTrimmed(textBlock);

            if (isTrimmed)
            {
                ToolTip currentToolTip;

                if (textBlock.ToolTip is ToolTip existingToolTip)
                {
                    currentToolTip = existingToolTip;

                    if (!existingToolTip.Content.Equals(textBlock.Text)) existingToolTip.Content = textBlock.Text;
                }
                else
                {
                    currentToolTip = new()
                    {
                        PlacementTarget = textBlock, // Устанавливаем PlacementTarget напрямую
                        Placement = PlacementMode.RelativePoint,
                        Content = textBlock.Text,
                        IsOpen = false
                    };

                    textBlock.ToolTip = currentToolTip;
                }

                if (mousePosition.HasValue)
                {
                    currentToolTip.HorizontalOffset = mousePosition.Value.X + 10;
                    currentToolTip.VerticalOffset = mousePosition.Value.Y + 10;
                }

                // Открываем ToolTip. Это будет инициировать его появление с заданным смещением. Последующие MouseMove будут его обновлять.
                if (!currentToolTip.IsOpen) currentToolTip.IsOpen = true;
            }
            else
            {
                if (textBlock.ToolTip is ToolTip toolTipInstance && toolTipInstance.IsOpen) toolTipInstance.IsOpen = false;

                textBlock.ToolTip = null;
            }
        }
        private static bool IsTextActuallyTrimmed(TextBlock textBlockToCheck)
        {
            if (textBlockToCheck == null || string.IsNullOrEmpty(textBlockToCheck.Text)) return false;

            if (textBlockToCheck.TextWrapping != TextWrapping.NoWrap) return false;

            if (double.IsNaN(textBlockToCheck.ActualWidth) || textBlockToCheck.ActualWidth <= 0) return false;

            FormattedText formattedText = new(
                textBlockToCheck.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                textBlockToCheck.FlowDirection,
                new Typeface(
                    textBlockToCheck.FontFamily,
                    textBlockToCheck.FontStyle,
                    textBlockToCheck.FontWeight,
                    textBlockToCheck.FontStretch),
                textBlockToCheck.FontSize,
                textBlockToCheck.Foreground,
                VisualTreeHelper.GetDpi(textBlockToCheck).PixelsPerDip
            );

            return formattedText.WidthIncludingTrailingWhitespace > textBlockToCheck.ActualWidth + 0.1;
        }
    }
}