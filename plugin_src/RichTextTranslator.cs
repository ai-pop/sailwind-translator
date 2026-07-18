using System.Collections.Generic;
using System.Text;

namespace SailwindTranslator
{
    /// <summary>
    /// Перевод текста с СОХРАНЕНИЕМ разделителей форматирования.
    ///
    /// Идея (от пользователя): вместо того чтобы пропускать форматированные строки
    /// (подписи настроек управления "Walk\tForward\tStop"), разделять их по
    /// разделителям, переводить каждый сегмент отдельно и собирать обратно,
    /// сохраняя вёрстку колонок.
    ///
    ///   "Walk\tForward\tStop"  ->  "Идти\tВперёд\tСтоп"
    ///   "pick up\nuse"         ->  "взять\nиспользовать"
    ///
    /// Разделители: \t (табуляция), \n (перенос), \r, \f, \v — всё, что отвечает за
    /// форматирование, а не за смысл. Сегменты между ними переводятся как обычные
    /// строки (через словарь + живой переводчик).
    /// </summary>
    public static class RichTextTranslator
    {
        private const string Separators = "\t\n\r\f\v";

        private struct Token
        {
            public string Text;
            public bool IsDelimiter;
        }

        /// <summary>
        /// Перевести строку, сохраняя разделители.
        /// out fullyTranslated = все сегменты переведены.
        /// out anyTranslated    = хотя бы один сегмент изменён (значит строка поменялась).
        /// Непереведённые сегменты автоматически ставятся в очередь LiveTranslator.
        /// </summary>
        public static string Translate(string text, out bool fullyTranslated, out bool anyTranslated)
        {
            fullyTranslated = true;
            anyTranslated = false;
            if (string.IsNullOrEmpty(text)) return text;

            // Быстрый путь: разделителей нет — один сегмент.
            bool hasSep = false;
            foreach (var c in text)
                if (Separators.IndexOf(c) >= 0) { hasSep = true; break; }

            if (!hasSep)
                return TranslateSegment(text, out fullyTranslated, out anyTranslated);

            // Медленный путь: токенизируем, переводим сегменты, собираем обратно.
            var tokens = SplitPreserving(text);
            var sb = new StringBuilder(text.Length + 8);
            foreach (var tok in tokens)
            {
                if (tok.IsDelimiter)
                {
                    sb.Append(tok.Text);
                }
                else
                {
                    bool segFull, segAny;
                    string segRu = TranslateSegment(tok.Text, out segFull, out segAny);
                    sb.Append(segRu);
                    if (!segFull) fullyTranslated = false;
                    if (segAny) anyTranslated = true;
                }
            }
            return sb.ToString();
        }

        private static string TranslateSegment(string seg, out bool fullyTranslated, out bool anyTranslated)
        {
            fullyTranslated = true;
            anyTranslated = false;
            if (string.IsNullOrEmpty(seg)) return seg;
            if (string.IsNullOrWhiteSpace(seg)) return seg;

            // Уже русский — не трогаем (значит, этот сегмент уже переведён ранее).
            if (ContainsCyrillic(seg)) { anyTranslated = true; return seg; }

            // Технический текст (клавиши, цифры, короткие строки без букв) — не переводим.
            if (LiveTranslator.ShouldSkip(seg)) return seg;

            // Есть в словаре (ручной или ранее закешированный перевод)?
            var ru = Plugin.Manager?.Get(seg);
            if (!string.IsNullOrEmpty(ru) && ru != seg)
            {
                anyTranslated = true;
                return ru;
            }

            // Нет перевода — ставим сегмент в очередь живому переводчику.
            fullyTranslated = false;
            LiveTranslator.Enqueue(seg);
            return seg; // пока оставляем оригинал, на следующем проходе подставится перевод
        }

        /// <summary>
        /// Разбивает строку на токены, ЧЕРЕДУЯ текстовые сегменты и разделители.
        /// Последовательные разделители не схлопываются — "Walk\t\tForward" даст
        /// [Walk, \t, \t, Forward], чтобы сохранить ширину колонки при сборке.
        /// </summary>
        private static List<Token> SplitPreserving(string text)
        {
            var tokens = new List<Token>();
            var current = new StringBuilder();
            foreach (var c in text)
            {
                if (Separators.IndexOf(c) >= 0)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(new Token { Text = current.ToString(), IsDelimiter = false });
                        current.Clear();
                    }
                    tokens.Add(new Token { Text = c.ToString(), IsDelimiter = true });
                }
                else
                {
                    current.Append(c);
                }
            }
            if (current.Length > 0)
                tokens.Add(new Token { Text = current.ToString(), IsDelimiter = false });
            return tokens;
        }

        private static bool ContainsCyrillic(string s)
        {
            if (s == null) return false;
            foreach (var c in s)
                if ((c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F')) return true;
            return false;
        }
    }
}
