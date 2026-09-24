// Пространство имён библиотеки (общее с ITextFileSolver.cs и LinqSolver.cs). Здесь НЕТ `using System.Linq;` — это принципиально: по ТЗ этот решатель не использует LINQ.
// (Implicit usings подключают System, System.IO, System.Collections.Generic — их достаточно для Dictionary/List/HashSet/File/Math.)
namespace Lab4.Solvers;

// Реализация ITextFileSolver без единого метода из System.Linq — только явные циклы
// и коллекции из System.Collections.Generic.
// Это «конкретная стратегия №2» паттерна «Стратегия»: результаты те же, что у LinqSolver, но считаются императивно — «по шагам».
// Используется: создаётся в MainForm.CreateSolver (Lab4.UI\MainForm.cs), когда в ComboBox выбран «без LINQ» (индекс 1); в тестах — SolversTests.Solvers().
public class NoLinqSolver : ITextFileSolver
{
    // Задача 1: самая частая пара соседних символов. Вызывается из MainForm.ShowPopularCharPairAsync (через интерфейс, внутри Task.Run).
    // Тесты: GetPopularCharPair_BothSolvers_ReturnSameResult, _DoesNotCrossLineBoundary, _WithinLineDeterministicWinner, _EmptyFiles_ReturnsDefault, _NoConsecutivePairs_ReturnsDefault, _NonExistentFile_DoesNotThrow.
    // Аналог в другом решателе: LinqSolver.cs : GetPopularCharPair (там SelectMany + GroupBy + OrderByDescending вместо циклов).
    public ValueTuple<char, char> GetPopularCharPair(string[] files)
    {
        // Защита: нет файлов — вернуть default, не бросать исключение.
        if (files is null || files.Length == 0)
        {
            // default = ('\0','\0'); MainForm понимает это как «пар нет».
            return default;
        } // конец if (нет файлов)

        // Счётчики пар. Ключ словаря — кортеж (char,char); ValueTuple сравнивается по значению, поэтому одинаковые пары из разных мест попадают в один ключ (в LinqSolver ту же роль играет GroupBy).
        var counts = new Dictionary<(char, char), int>();
        // Порядок первого появления пары нужен для устойчивого выбора при равенстве частот.
        // Причина: порядок перебора Dictionary мы не используем, а список firstSeenOrder гарантирует «побеждает пара, встретившаяся раньше» — как устойчивая сортировка в LinqSolver.
        var firstSeenOrder = new List<(char, char)>();

        // Внешний цикл по файлам (fi = file index).
        for (int fi = 0; fi < files.Length; fi++)
        {
            // Читаем строки файла безопасно (TryReadLines ниже); при ошибке — пустой массив, цикл по строкам не выполнится.
            string[] lines = TryReadLines(files[fi]);
            // Цикл по строкам файла (li = line index). Строки обрабатываются по отдельности -> пара не пересекает границу строк.
            for (int li = 0; li < lines.Length; li++)
            {
                // Приводим строку к нижнему регистру (без учёта регистра); аналог `.Select(line => line.ToLowerInvariant())` в LinqSolver.
                string line = lines[li].ToLowerInvariant();
                // Цикл по позициям пар: условие i+1 < Length гарантирует, что второй символ пары существует (аналог Enumerable.Range(0, Length-1) в LinqSolver).
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    // Создаём кортеж-пару из двух соседних символов (аналог `(First: line[i], Second: line[i+1])` в LinqSolver, но без имён элементов).
                    var pair = (line[i], line[i + 1]);
                    // TryGetValue: если пара уже встречалась — в c текущий счётчик, вернёт true.
                    if (counts.TryGetValue(pair, out int c))
                    {
                        // Пара уже известна — увеличиваем счётчик на 1.
                        counts[pair] = c + 1;
                    } // конец if (пара уже была)
                    else
                    {
                        // Первая встреча пары — счётчик 1...
                        counts[pair] = 1;
                        // ...и запоминаем порядок её первого появления.
                        firstSeenOrder.Add(pair);
                    } // конец else (новая пара)
                } // конец цикла по позициям
            } // конец цикла по строкам
        } // конец цикла по файлам

        // Ни одной пары не найдено (пустые файлы/строки по 1 символу/файлы не читаются) — default.
        if (firstSeenOrder.Count == 0)
        {
            // default = ('\0','\0'); тесты _NoConsecutivePairs_ReturnsDefault, _EmptyFiles_..., _NonExistentFile_... сверяют это с LinqSolver.
            return default;
        } // конец if (пар нет)

        // Ручной поиск максимума. Начинаем с самой первой пары как с текущего «лучшего» варианта.
        var best = firstSeenOrder[0];
        // Её частота.
        int bestCount = counts[best];
        // Идём по остальным парам в порядке первого появления (аналог GroupBy + OrderByDescending + First в LinqSolver).
        for (int i = 1; i < firstSeenOrder.Count; i++)
        {
            // Очередная пара-кандидат.
            var pair = firstSeenOrder[i];
            // Её частота.
            int c = counts[pair];
            // Строгое `>` (а не `>=`): при равенстве частот остаётся более ранняя пара — так реализована «устойчивость» вручную.
            if (c > bestCount)
            {
                // Нашли пару чаще — запоминаем её...
                best = pair;
                // ...и её частоту.
                bestCount = c;
            } // конец if (нашли лучше)
        } // конец цикла поиска максимума

        // Возвращаем победителя (кортеж (char,char) уже нужного типа). Идёт в MainForm.ShowPopularCharPairAsync -> таблица и график.
        return best;
    } // конец метода GetPopularCharPair

    // Задача 2: N самых длинных слов. Вызывается из MainForm.ShowLongWordsAsync (Task.Run).
    // Тесты: GetLongWords_BothSolvers_ReturnSameResult, _StableOrderOnTies, _IsCaseInsensitive, _NonPositiveN_ReturnsEmptyList, _EmptyFolder_ReturnsEmptyList.
    // Аналог в другом решателе: LinqSolver.cs : GetLongWords (SelectMany + OrderByDescending + Take + ToList).
    public IList<string> GetLongWords(string[] files, char[] delimiters, int N)
    {
        // Результирующий список; заводим сразу, чтобы вернуть пустым при неверных входных данных.
        var result = new List<string>();
        // Защита: нет файлов/разделителей или N<=0 — вернуть пустой список (не null).
        if (files is null || files.Length == 0 || delimiters is null || N <= 0)
        {
            // Пустой список; тесты _NonPositiveN_ и _EmptyFolder_ проверяют Assert.Empty.
            return result;
        } // конец if (плохие входные данные)

        // Собираем все слова по всем файлам в порядке обнаружения.
        // Аналог `.SelectMany(TryReadAllText).SelectMany(Split).Select(ToLowerInvariant)` в LinqSolver.
        var words = new List<string>();
        // Цикл по файлам.
        for (int fi = 0; fi < files.Length; fi++)
        {
            // Текст файла целиком (при ошибке чтения — пустая строка).
            string text = TryReadAllText(files[fi]);
            // Разбиваем на слова по разделителям; RemoveEmptyEntries выбрасывает пустые токены (это метод string, не LINQ).
            string[] tokens = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            // Цикл по словам файла.
            for (int i = 0; i < tokens.Length; i++)
            {
                // Добавляем слово в нижнем регистре в общий список: порядок = файлы по очереди, слова слева направо.
                words.Add(tokens[i].ToLowerInvariant());
            } // конец цикла по словам
        } // конец цикла по файлам

        // Слов нет вообще — вернуть пустой список.
        if (words.Count == 0)
        {
            // Пустой result.
            return result;
        } // конец if (слов нет)

        // Индекс нужен для устойчивости сортировки при равной длине слов.
        // Почему это нужно: List<T>.Sort — НЕУСТОЙЧИВАЯ сортировка (равные по ключу элементы могут поменяться местами), а OrderByDescending в LinqSolver устойчив сам по себе.
        // Поэтому запоминаем номер слова и при равной длине сравниваем по этому номеру — и получаем тот же результат, что у LinqSolver.
        // Список кортежей с именованными элементами (Word, Index); ёмкость сразу равна числу слов (чтобы не перевыделять память).
        var indexed = new List<(string Word, int Index)>(words.Count);
        // Цикл по всем словам.
        for (int i = 0; i < words.Count; i++)
        {
            // Кладём кортеж (слово, его порядковый номер обнаружения).
            indexed.Add((words[i], i));
        } // конец цикла индексации

        // Sort принимает делегат сравнения Comparison<T> — здесь это лямбда (a, b) => ...; она сравнивает два элемента и возвращает <0, 0 или >0.
        // Аналог в другом решателе: LinqSolver.cs : GetLongWords — `.OrderByDescending(w => w.Length)`.
        indexed.Sort((a, b) =>
        {
            // Сначала сравниваем длины «в обратном порядке» (b с a): чем длиннее слово, тем оно раньше -> убывание длины.
            int byLength = b.Word.Length.CompareTo(a.Word.Length); // по убыванию длины
            // Если длины разные — результат по длине; иначе сравниваем индексы обнаружения по возрастанию (кто раньше найден — тот раньше в списке). Это и есть ручная устойчивость.
            return byLength != 0 ? byLength : a.Index.CompareTo(b.Index); // при равенстве — порядок обнаружения
        }); // конец лямбды-компаратора

        // Сколько слов брать: не больше N и не больше, чем всего слов (аналог Take(N) в LinqSolver, который сам безопасен при N > количества).
        int take = Math.Min(N, indexed.Count);
        // Цикл по первым `take` элементам уже отсортированного списка.
        for (int i = 0; i < take; i++)
        {
            // Копируем только слово (индекс больше не нужен) в результат.
            result.Add(indexed[i].Word);
        } // конец цикла копирования

        // Готовый список самых длинных слов -> MainForm.ShowLongWordsAsync (таблица №/Слово/Длина и график).
        return result;
    } // конец метода GetLongWords

    // Задача 3: файл с наибольшим числом вхождений общеупотребимых слов. Вызывается из MainForm.ShowFileWithManyCommonWordsAsync (Task.Run).
    // Тесты: GetFileWithManyCommonWords_BothSolvers_ReturnSameResult, _NoCommonWords_ReturnsDefault, _NonPositiveK_ReturnsDefault, _EmptyFiles_ReturnsDefault.
    // Аналог в другом решателе: LinqSolver.cs : GetFileWithManyCommonWords (Select/Distinct/Where+All/Sum/Max/First).
    public ValueTuple<string, int> GetFileWithManyCommonWords(string[] files, char[] delimiters, int K)
    {
        // Защита входных данных.
        if (files is null || files.Length == 0 || delimiters is null || K <= 0)
        {
            // default = (null, 0): «общих слов нет».
            return default;
        } // конец if (плохие входные данные)

        // Для каждого файла — свой словарь «слово -> количество». perFileCounts[fi] соответствует files[fi]. Аналог `files.Select(f => BuildWordCounts(...)).ToList()` в LinqSolver.
        var perFileCounts = new List<Dictionary<string, int>>(files.Length);
        // Цикл по файлам.
        for (int fi = 0; fi < files.Length; fi++)
        {
            // Строим словарь слов файла и добавляем в список.
            perFileCounts.Add(BuildWordCounts(files[fi], delimiters));
        } // конец цикла по файлам

        // Собираем множество всех различных слов, встречающихся хоть в одном файле.
        // HashSet сам отбрасывает дубликаты — это аналог `.SelectMany(d => d.Keys).Distinct()` в LinqSolver.
        var allWords = new HashSet<string>();
        // Цикл по словарям файлов.
        for (int fi = 0; fi < perFileCounts.Count; fi++)
        {
            // Перебираем слова (ключи) словаря одного файла.
            foreach (string w in perFileCounts[fi].Keys)
            {
                // Add игнорирует слово, если оно уже есть.
                allWords.Add(w);
            } // конец foreach по словам
        } // конец цикла по словарям

        // Список общеупотребимых слов (слово в КАЖДОМ файле не менее K раз).
        var commonWords = new List<string>();
        // Перебираем все различные слова (аналог `.Where(...)` в LinqSolver).
        foreach (string w in allWords)
        {
            // Предполагаем, что слово общеупотребимо; при первом же нарушении сбросим флаг.
            bool isCommon = true;
            // Проверяем слово во всех файлах (аналог `perFileCounts.All(...)` в LinqSolver).
            for (int fi = 0; fi < perFileCounts.Count; fi++)
            {
                // Слова нет в этом файле ИЛИ оно встречается меньше K раз -> не общеупотребимо. (`||` короткого замыкания: если TryGetValue false, `c` не читается.)
                if (!perFileCounts[fi].TryGetValue(w, out int c) || c < K)
                {
                    // Снимаем флаг...
                    isCommon = false;
                    // ...и прекращаем проверять остальные файлы (аналог короткого замыкания All в LinqSolver).
                    break;
                } // конец if (нарушение условия)
            } // конец цикла по файлам

            // Слово прошло проверку во всех файлах.
            if (isCommon)
            {
                // Добавляем в общий список.
                commonWords.Add(w);
            } // конец if (isCommon)
        } // конец foreach по словам

        // Общих слов нет — default (null, 0).
        if (commonWords.Count == 0)
        {
            // default; тест _NoCommonWords_ReturnsDefault.
            return default;
        } // конец if (общих слов нет)

        // Ищем файл с максимальной суммой. Стартуем с первого файла и суммы -1 (любая настоящая сумма >= 0 её обгонит; это аналог Max + First в LinqSolver).
        string bestFile = files[0];
        // Лучшая сумма пока «минус единица».
        int bestTotal = -1;
        // Цикл по файлам.
        for (int fi = 0; fi < files.Length; fi++)
        {
            // Сумма вхождений общих слов в текущем файле (аналог `Sum` внутри Select с индексом в LinqSolver).
            int total = 0;
            // Цикл по общим словам.
            for (int wi = 0; wi < commonWords.Count; wi++)
            {
                // Достаём счётчик слова в словаре ЭТОГО файла.
                if (perFileCounts[fi].TryGetValue(commonWords[wi], out int c))
                {
                    // Прибавляем к сумме файла.
                    total += c;
                } // конец if (слово есть)
            } // конец цикла по словам

            // Строгое `>`: при равных суммах побеждает файл, идущий раньше в массиве (как `First` в LinqSolver).
            if (total > bestTotal)
            {
                // Запоминаем лучшую сумму...
                bestTotal = total;
                // ...и файл.
                bestFile = files[fi];
            } // конец if (нашли лучше)
        } // конец цикла по файлам

        // Возвращаем (путь, сумма) -> MainForm.ShowFileWithManyCommonWordsAsync (таблица и график).
        return (bestFile, bestTotal);
    } // конец метода GetFileWithManyCommonWords

    // Задача 4: распределение знаков пунктуации по возрастанию частоты. Вызывается из MainForm.ShowPunctuationDistributionAsync (Task.Run).
    // Тесты: GetPunctuationDistribution_BothSolvers_ReturnSameResult, _IncludesAllCharsEvenWithZeroFrequency, _EmptyPunctuationArray_ReturnsEmptyDictionary.
    // Аналог в другом решателе: LinqSolver.cs : GetPunctuationDistribution (Select с индексом, GroupBy, OrderBy/ThenBy).
    public IDictionary<char, int> GetPunctuationDistribution(string[] files, char[] punctuations)
    {
        // Результат-словарь; порядок вставки = порядок перебора (используется в MainForm.ShowPunctuationDistributionAsync).
        var result = new Dictionary<char, int>();
        // Нет набора знаков — пустой словарь.
        if (punctuations is null || punctuations.Length == 0)
        {
            // Пустой словарь; тест _EmptyPunctuationArray_ReturnsEmptyDictionary.
            return result;
        } // конец if (нет знаков)

        // Убираем дубликаты символов, сохраняя первое вхождение и его исходный индекс.
        // Аналог `Select((ch, idx) => ...).GroupBy(...).Select(g => g.First())` в LinqSolver.
        // Уникальные знаки в порядке первого появления.
        var distinctChars = new List<char>();
        // «знак -> его первая позиция в исходном массиве» (нужна для тай-брейка, как Index в LinqSolver).
        var firstIndex = new Dictionary<char, int>();
        // Цикл по исходному массиву знаков.
        for (int i = 0; i < punctuations.Length; i++)
        {
            // Текущий знак.
            char ch = punctuations[i];
            // Если знак ещё не встречался...
            if (!firstIndex.ContainsKey(ch))
            {
                // ...запоминаем его первую позицию...
                firstIndex[ch] = i;
                // ...и добавляем в список уникальных.
                distinctChars.Add(ch);
            } // конец if (новый знак)
        } // конец цикла дедупликации

        // Счётчики «знак -> число вхождений».
        var counts = new Dictionary<char, int>();
        // Цикл по уникальным знакам.
        for (int i = 0; i < distinctChars.Count; i++)
        {
            // Стартуем с 0: знаки, которых нет в текстах, всё равно попадут в результат.
            counts[distinctChars[i]] = 0;
        } // конец цикла обнуления

        // files == null — просто не считаем, все частоты останутся 0.
        if (files is not null)
        {
            // Цикл по файлам.
            for (int fi = 0; fi < files.Length; fi++)
            {
                // Весь текст файла (пустая строка при ошибке чтения).
                string text = TryReadAllText(files[fi]);
                // Цикл по символам текста.
                for (int i = 0; i < text.Length; i++)
                {
                    // Текущий символ.
                    char ch = text[i];
                    // Ключ есть в counts только у знаков пунктуации (аналог `punctSet.Contains(ch)` в LinqSolver).
                    if (counts.ContainsKey(ch))
                    {
                        // Увеличиваем счётчик знака.
                        counts[ch]++;
                    } // конец if (это знак)
                } // конец цикла по символам
            } // конец цикла по файлам
        } // конец if (files не null)

        // Сортируем по возрастанию частоты, при равенстве — по исходному порядку в punctuations.
        // Копия списка уникальных знаков, чтобы сортировать её, а distinctChars не портить.
        var ordered = new List<char>(distinctChars);
        // Ручной компаратор (делегат Comparison<char> в виде лямбды); аналог `OrderBy(counts).ThenBy(Index)` в LinqSolver.
        ordered.Sort((a, b) =>
        {
            // Сравниваем частоты по возрастанию.
            int byCount = counts[a].CompareTo(counts[b]);
            // Частоты разные — решает частота; равны — решает первая позиция в исходном массиве. Ключи полностью различны, поэтому неустойчивость Sort не мешает.
            return byCount != 0 ? byCount : firstIndex[a].CompareTo(firstIndex[b]);
        }); // конец лямбды-компаратора

        // Цикл по отсортированным знакам.
        for (int i = 0; i < ordered.Count; i++)
        {
            // Текущий знак.
            char ch = ordered[i];
            // Вставляем в результат в порядке возрастания частоты (порядок вставки Dictionary = порядок вывода).
            result[ch] = counts[ch];
        } // конец цикла заполнения

        // Готовый словарь -> MainForm.ShowPunctuationDistributionAsync (таблица «Знак/Частота» и график).
        return result;
    } // конец метода GetPunctuationDistribution

    // Безопасно читает строки файла; при ошибке возвращает пустой массив (без исключений наружу).
    // Используется: в этом файле GetPopularCharPair (`TryReadLines(files[fi])`).
    // Аналог в другом решателе: LinqSolver.cs : TryReadLines (там IEnumerable<string>).
    private static string[] TryReadLines(string path)
    {
        // Безопасное чтение: исключения файловой системы не должны выходить наружу (ТЗ).
        try
        {
            // Все строки файла.
            return File.ReadAllLines(path);
        } // конец try
        catch
        {
            // Ошибка -> пустой массив: цикл по строкам просто ничего не сделает. Тесты *_NonExistentFile_DoesNotThrow.
            return Array.Empty<string>();
        } // конец catch
    } // конец метода TryReadLines

    // Безопасно читает содержимое файла целиком; при ошибке возвращает пустую строку.
    // Используется: в этом файле GetLongWords, GetPunctuationDistribution и BuildWordCounts.
    // Аналог в другом решателе: LinqSolver.cs : TryReadAllText (там IEnumerable<string> из 0 или 1 элемента).
    private static string TryReadAllText(string path)
    {
        // Безопасное чтение файла.
        try
        {
            // Весь текст файла.
            return File.ReadAllText(path);
        } // конец try
        catch
        {
            // Ошибка -> пустая строка: Split даст 0 слов, цикл по символам не выполнится.
            return string.Empty;
        } // конец catch
    } // конец метода TryReadAllText

    // Строит словарь «слово -> число вхождений» одного файла (слова в нижнем регистре).
    // Используется: в этом файле GetFileWithManyCommonWords (`BuildWordCounts(files[fi], delimiters)`).
    // Аналог в другом решателе: LinqSolver.cs : BuildWordCounts (через SelectMany/Select).
    private static Dictionary<string, int> BuildWordCounts(string file, char[] delimiters)
    {
        // Пустой словарь-накопитель.
        var dict = new Dictionary<string, int>();
        // Текст файла.
        string text = TryReadAllText(file);
        // Слова файла (пустые токены отброшены).
        string[] tokens = text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        // Цикл по словам.
        for (int i = 0; i < tokens.Length; i++)
        {
            // Слово в нижнем регистре.
            string w = tokens[i].ToLowerInvariant();
            // Есть -> +1, нет -> 1 (`out int c` получает текущее значение счётчика).
            dict[w] = dict.TryGetValue(w, out int c) ? c + 1 : 1;
        } // конец цикла по словам

        // Возвращаем словарь -> perFileCounts в GetFileWithManyCommonWords.
        return dict;
    } // конец метода BuildWordCounts
} // конец класса NoLinqSolver
