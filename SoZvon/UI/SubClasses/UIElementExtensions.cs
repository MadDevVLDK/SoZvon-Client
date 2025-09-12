using System.Windows;
using System.Windows.Media;

namespace SoZvon.UI.SubClasses
{
    public static class UIElementExtensions
    {
        /// <summary>
        /// Рекурсивно ищет UIElement по его Tag в визуальном дереве, начиная с текущего элемента.
        /// </summary>
        /// <typeparam name="T">Тип элемента, который нужно найти (например, Button, TextBlock, UIElement).</typeparam>
        /// <param name="parent">Элемент, с которого начать поиск (этот метод расширяет DependencyObject).</param>
        /// <param name="tagToFind">Значение Tag, которое нужно найти.</param>
        /// <returns>Найденный элемент указанного типа T, или null, если не найден.</returns>
        public static T? FindElementByTag<T>(this DependencyObject parent, string tagToFind) where T : FrameworkElement
        {
            // Проверяем текущий элемент
            if (parent is T frameworkElement && frameworkElement.Tag != null && frameworkElement.Tag.ToString() == tagToFind)
            {
                return frameworkElement;
            }

            // Обходим дочерние элементы визуального дерева
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T? foundChild = FindElementByTag<T>(child, tagToFind); // Рекурсивный вызов
                
                if (foundChild != null) return foundChild;
            }
            return null;
        }

        // Также можно добавить метод для получения всех элементов по тегу
        public static IEnumerable<T> FindElementsByTag<T>(this DependencyObject parent, string tagToFind) where T : FrameworkElement
        {
            // Проверяем текущий элемент
            if (parent is T frameworkElement && frameworkElement.Tag != null && frameworkElement.Tag.ToString() == tagToFind)
            {
                yield return frameworkElement;
            }

            // Обходим дочерние элементы визуального дерева
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                foreach (T foundChild in FindElementsByTag<T>(child, tagToFind)) // Рекурсивный вызов
                {
                    yield return foundChild;
                }
            }
        }


        // ЕЩЕ БОЛЕЕ ГИБКИЙ ПОДХОД: Расширение Descendants() для LINQ (как в Подходе 3 из предыдущего ответа)
        /// <summary>
        /// Возвращает плоский список всех визуальных потомков элемента.
        /// </summary>
        public static IEnumerable<DependencyObject> Descendants(this DependencyObject parent)
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                foreach (var grandChild in child.Descendants())
                {
                    yield return grandChild;
                }
            }
        }

        /// <summary>
        /// Ищет первый элемент с заданным Tag, используя LINQ.
        /// </summary>
        /// <typeparam name="T">Тип элемента, который нужно найти.</typeparam>
        /// <param name="parent">Элемент, с которого начать поиск.</param>
        /// <param name="tagToFind">Значение Tag для поиска.</param>
        /// <returns>Первый найденный элемент или null.</returns>
        public static T? FindFirstElementByTagLinq<T>(this DependencyObject parent, string tagToFind) where T : FrameworkElement
        {
            return parent.Descendants().OfType<T>().FirstOrDefault(e => e.Tag != null && e.Tag.ToString() == tagToFind);
        }

        /// <summary>
        /// Ищет все элементы с заданным Tag, используя LINQ.
        /// </summary>
        /// <typeparam name="T">Тип элементов, которые нужно найти.</typeparam>
        /// <param name="parent">Элемент, с которого начать поиск.</param>
        /// <param name="tagToFind">Значение Tag для поиска.</param>
        /// <returns>Коллекция найденных элементов.</returns>
        public static IEnumerable<T> FindAllElementsByTagLinq<T>(this DependencyObject parent, string tagToFind) where T : FrameworkElement
        {
            return parent.Descendants().OfType<T>().Where(e => e.Tag != null && e.Tag.ToString() == tagToFind);
        }
    }
}
