// Подключает System.Linq — методы расширения SelectMany/Select/GroupBy/OrderBy/Where/All/Sum/Max/First и т.д. (в .NET 10 они уже есть в implicit usings, но using оставлен явно, чтобы было видно «это LINQ-решатель»).
// Используется: во всём этом файле; в NoLinqSolver.cs этого using нет специально — там LINQ запрещён.
using System.Linq;

// Пространство имён библиотеки (общее с ITextFileSolver.cs и NoLinqSolver.cs).
namespace Lab4.Solvers;

// Реализация ITextFileSolver с использованием методов System.Linq.
// Это «конкретная стратегия №1» паттерна «Стратегия»: решает те же 4 задачи, что NoLinqSolver, но цепочками LINQ («что получить»), а не циклами («как считать»).
// Используется: создаётся в MainForm.CreateSolver (Lab4.UI\MainForm.cs), когда в ComboBox выбран пункт «LINQ» (индекс 0); в тестах — SolversTests.Solvers().
public class LinqSolver : ITextFileSolver
{
    // Задача 1: самая частая пара соседних символов. Вызывается из MainForm.ShowPopularCharPairAsync (через интерфейс, внутри Task.Run).
    // Тесты: GetPopularCharPair_BothSolvers_ReturnSameResult, _DoesNotCrossLineBoundary, _WithinLineDeterministicWinner, _EmptyFiles_ReturnsDefault, _NoConsecutivePairs_ReturnsDefault, _NonExistentFile_DoesNotThrow.
    // Аналог в другом решателе: NoLinqSolver.cs : GetPopularCharPair (там цикл + Dictionary вместо GroupBy).
    public ValueTuple<char, char> GetPopularCharPair(string[] files)
    {
        // Защита: нет массива или он пуст — задача неразрешима, по ТЗ возвращаем default, а не исключение.
        if (files is null || files.Length == 0)
        {
            // default для ValueTuple<char,char> = ('\0','\0'); MainForm.ShowPopularCharPairAsync проверяет `pair == default` и пишет «Пары символов не найдены».
            return default;
        } // конец if (нет файлов)

        // Пары считаются только внутри одной строки (граница строк пары не образует).
        // Ниже — конвейер LINQ. ВАЖНО: до вызова ToList() ничего не выполняется (отложенное выполнение), это лишь «рецепт».
        // Простыми словами: файлы -> все строки всех файлов -> строки в нижнем регистре -> для каждой строки все пары соседних символов -> в список.
        // Аналог в другом решателе: NoLinqSolver.cs : GetPopularCharPair — там три вложенных for (файлы, строки, позиции) и сразу подсчёт.
        var pairs = files
            // SelectMany = «сплющивание»: на каждый путь к файлу TryReadLines даёт набор строк, а SelectMany склеивает эти наборы в одну плоскую последовательность строк. Передана группа методов (без лямбды) — это делегат Func<string,IEnumerable<string>>.
            .SelectMany(TryReadLines)
            // Select = «преобразовать каждый элемент»: каждую строку в нижний регистр (инвариантная культура — результат не зависит от региона ОС), так учитывается требование «без учёта регистра».
            .Select(line => line.ToLowerInvariant())
            // Второй SelectMany: каждая строка превращается в набор пар, и все наборы склеиваются. Лямбда `line => ...` — анонимный метод; `line` — параметр.
            // Enumerable.Range(0, n) даёт числа 0..n-1 (индексы первых символов пар); Math.Max(0, ...) защищает от отрицательного количества у пустой строки (длина 0 -> -1 -> 0 пар).
            .SelectMany(line => Enumerable.Range(0, Math.Max(0, line.Length - 1))
                // Select для каждого индекса i строит кортеж (First: line[i], Second: line[i+1]). Именованные элементы First/Second — просто удобные имена полей ValueTuple; тип кортежа (char First, char Second). Лямбда захватывает внешнюю переменную line (замыкание).
                .Select(i => (First: line[i], Second: line[i + 1])))
            // ToList() — немедленное выполнение: тут конвейер реально запускается, файлы читаются, пары создаются и сохраняются в List.
            .ToList();

        // Пар нет вообще (файлы пустые/не читаются/строки по 1 символу) — возвращаем default.
        if (pairs.Count == 0)
        {
            // Значение по умолчанию ('\0','\0'): та же ситуация проверяется тестами _NoConsecutivePairs_ReturnsDefault и _DoesNotCrossLineBoundary.
            return default;
        } // конец if (пар нет)

        // Ищем пару с наибольшей частотой.
        // Аналог в другом решателе: NoLinqSolver.cs : GetPopularCharPair — Dictionary counts + список firstSeenOrder и ручной поиск максимума.
        var best = pairs
            // GroupBy(p => p) объединяет одинаковые пары в группы: ключ группы = сама пара, содержимое = все её вхождения. Кортежи сравниваются по значению, поэтому ('a','a') из разных мест попадут в одну группу. Группы идут в порядке ПЕРВОГО появления ключа.
            .GroupBy(p => p)
            // Сортируем группы по убыванию количества элементов (g.Count() — частота пары). OrderByDescending устойчив: при равных частотах группа, которая встретилась раньше, остаётся раньше (это то же правило «побеждает первая», что и в NoLinqSolver).
            .OrderByDescending(g => g.Count()) // OrderByDescending — устойчивая сортировка
            // First() — немедленно берёт первую группу = самую частую (при равенстве — раннюю).
            .First()
            // Key — ключ группы, т.е. сама пара (First, Second).
            .Key;

        // Возвращаем обычный (char, char): из именованного кортежа (First, Second) переносим значения в результат.
        // Результат уходит в MainForm.ShowPopularCharPairAsync -> таблица и график.
        return (best.First, best.Second);
    } // конец метода GetPopularCharPair

    // Задача 2: N самых длинных слов. Вызывается из MainForm.ShowLongWordsAsync (Task.Run).
    // Тесты: GetLongWords_BothSolvers_ReturnSameResult, _StableOrderOnTies, _IsCaseInsensitive, _NonPositiveN_ReturnsEmptyList, _EmptyFolder_ReturnsEmptyList.
    // Аналог в другом решателе: NoLinqSolver.cs : GetLongWords (список + ручной List.Sort с индексом).
    public IList<string> GetLongWords(string[] files, char[] delimiters, int N)
    {
        // Защита от некорректных входных данных (null, пустой массив, N<=0) — возвращаем пустой список, а не null и не исключение (по ТЗ).
        if (files is null || files.Length == 0 || delimiters is null || N <= 0)
        {
            // Пустой List<string>: MainForm.ShowLongWordsAsync проверяет result.Count == 0 и показывает сообщение; тесты _NonPositiveN_ и _EmptyFolder_ проверяют Assert.Empty.
            return new List<string>();
        } // конец if (плохие входные данные)

        // Строим «рецепт» получения слов (отложенное выполнение — сам запрос ещё НЕ выполнен, файлы не читаются).
        // Выполнится он позже, на ToList() в конце метода. Если бы файл изменили между этой строкой и ToList(), в результат попали бы новые данные — это и есть отложенное выполнение.
        var words = files
            // Каждый путь -> одноэлементный набор {весь текст файла} (см. TryReadAllText), SelectMany склеивает их в последовательность текстов.
            .SelectMany(TryReadAllText)
            // Каждый текст -> массив слов: Split по разделителям, RemoveEmptyEntries отбрасывает пустые куски (два разделителя подряд), а SelectMany склеивает массивы слов всех файлов. Порядок: файлы по очереди, слова внутри файла слева направо.
            .SelectMany(text => text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries))
            // Каждое слово в нижний регистр (без учёта регистра по ТЗ).
            .Select(w => w.ToLowerInvariant());

        // OrderByDescending — устойчивая сортировка, при равной длине сохраняется порядок обнаружения.
        // Ниже конвейер продолжается и запускается целиком (ToList — немедленное выполнение).
        // Аналог в другом решателе: NoLinqSolver.cs : GetLongWords — там устойчивость достигается вручную, через индекс обнаружения в компараторе.
        return words
            // Сортировка по убыванию длины слова. Устойчивость: слова одинаковой длины остаются в исходном порядке — именно её проверяет тест GetLongWords_StableOrderOnTies.
            .OrderByDescending(w => w.Length)
            // Take(N) — оставить первые N элементов (или меньше, если слов меньше N).
            .Take(N)
            // ToList() — запуск всего запроса и превращение в List<string> (он реализует IList<string>).
            .ToList();
    } // конец метода GetLongWords

    // Задача 3: файл с наибольшим числом вхождений общеупотребимых слов. Вызывается из MainForm.ShowFileWithManyCommonWordsAsync (Task.Run).
    // Тесты: GetFileWithManyCommonWords_BothSolvers_ReturnSameResult, _NoCommonWords_ReturnsDefault, _NonPositiveK_ReturnsDefault, _EmptyFiles_ReturnsDefault.
    // Аналог в другом решателе: NoLinqSolver.cs : GetFileWithManyCommonWords (вложенные циклы, HashSet, флаг isCommon).
    public ValueTuple<string, int> GetFileWithManyCommonWords(string[] files, char[] delimiters, int K)
    {
        // Защита входных данных: нет файлов, нет разделителей или K<=0 — вернуть default.
        if (files is null || files.Length == 0 || delimiters is null || K <= 0)
        {
            // default для (string,int) = (null, 0); MainForm проверяет string.IsNullOrEmpty(result.Item1) и пишет «Общеупотребимые слова не найдены».
            return default;
        } // конец if (плохие входные данные)

        // Словарь "слово -> число вхождений" для каждого файла.
        // perFileCounts[i] — словарь для files[i]. ToList() здесь важен: он выполняет запрос СРАЗУ и запоминает результат, чтобы не читать файлы заново при каждом обращении (иначе отложенный запрос перечитывал бы файлы много раз).
        var perFileCounts = files
            // Для каждого пути строим словарь слов (BuildWordCounts ниже в этом же файле).
            .Select(f => BuildWordCounts(f, delimiters))
            // Немедленное выполнение и сохранение в List<Dictionary<string,int>>.
            .ToList();

        // Все различные слова из всех файлов.
        var allWords = perFileCounts
            // Из каждого словаря берём ключи (слова) и склеиваем в одну последовательность.
            .SelectMany(d => d.Keys)
            // Distinct — убирает повторы слов, встречающихся в нескольких файлах (запрос ленивый, пока не перечислят).
            .Distinct();

        // Слово общеупотребимо, если оно встречается в каждом файле не менее K раз.
        // Здесь лямбда `w => perFileCounts.All(...)` захватывает внешние переменные perFileCounts и K (замыкание): они не параметры лямбды, но доступны внутри неё.
        var commonWords = allWords
            // Where — оставляем только те слова, для которых лямбда вернула true. All(...) = «условие верно для ВСЕХ словарей (файлов)».
            // d.TryGetValue(w, out int c) — пробуем достать счётчик слова w из словаря файла: true, если слово в файле есть; `&& c >= K` — и оно встречается не менее K раз.
            .Where(w => perFileCounts.All(d => d.TryGetValue(w, out int c) && c >= K))
            // Запуск запроса и сохранение результата (здесь выполняется и Distinct, и Where).
            .ToList();

        // Общих слов нет — возвращаем default (тест _NoCommonWords_ReturnsDefault).
        if (commonWords.Count == 0)
        {
            // default (null, 0).
            return default;
        } // конец if (общих слов нет)

        // Для каждого файла считаем сумму его собственных счётчиков по общим словам.
        var totals = files
            // Select с ИНДЕКСОМ: лямбда получает пару (элемент, его номер) — f это путь, idx — позиция файла, совпадающая с позицией словаря в perFileCounts.
            .Select((f, idx) => (
                // Именованный элемент кортежа File: путь к файлу.
                File: f,
                // Именованный элемент Total: Sum складывает для всех общих слов их количество в ЭТОМ файле (perFileCounts[idx]); если слова вдруг нет — 0 (тернарный оператор ? :).
                Total: commonWords.Sum(w => perFileCounts[idx].TryGetValue(w, out int c) ? c : 0)))
            // Запуск запроса и сохранение списка кортежей (File, Total).
            .ToList();

        // Max — максимальная сумма среди файлов (немедленное выполнение, возвращает одно число).
        var maxTotal = totals.Max(t => t.Total);
        // First — первый файл с такой суммой: при равенстве побеждает файл, идущий раньше в массиве files (в NoLinqSolver то же правило дают строгий `>` и порядок обхода).
        var winner = totals.First(t => t.Total == maxTotal);

        // Возвращаем (путь, сумма). Идёт в MainForm.ShowFileWithManyCommonWordsAsync -> таблица и график.
        return (winner.File, winner.Total);
    } // конец метода GetFileWithManyCommonWords

    // Задача 4: распределение знаков пунктуации по возрастанию частоты. Вызывается из MainForm.ShowPunctuationDistributionAsync (Task.Run).
    // Тесты: GetPunctuationDistribution_BothSolvers_ReturnSameResult, _IncludesAllCharsEvenWithZeroFrequency, _EmptyPunctuationArray_ReturnsEmptyDictionary.
    // Аналог в другом решателе: NoLinqSolver.cs : GetPunctuationDistribution (List + Dictionary firstIndex + List.Sort с компаратором).
    public IDictionary<char, int> GetPunctuationDistribution(string[] files, char[] punctuations)
    {
        // Результирующий словарь. Dictionary при простой вставке без удалений перечисляется в порядке вставки — на этом строится «упорядоченность» результата (см. MainForm.ShowPunctuationDistributionAsync: foreach по словарю).
        var result = new Dictionary<char, int>();
        // Нет набора знаков — вернуть пустой словарь (не null).
        if (punctuations is null || punctuations.Length == 0)
        {
            // Пустой словарь; тест _EmptyPunctuationArray_ReturnsEmptyDictionary проверяет Assert.Empty.
            return result;
        } // конец if (нет знаков)

        // Убираем дубликаты символов, сохраняя первое вхождение и его позицию.
        // Аналог в другом решателе: NoLinqSolver.cs : GetPunctuationDistribution — там для этого List distinctChars + Dictionary firstIndex.
        var distinctPunctuations = punctuations
            // Select с индексом: превращаем каждый символ в кортеж (Char: символ, Index: его позиция в исходном массиве).
            .Select((ch, idx) => (Char: ch, Index: idx))
            // GroupBy по символу: одинаковые знаки попадают в одну группу; порядок групп — по первому появлению.
            .GroupBy(p => p.Char)
            // Из каждой группы берём первый кортеж = первое вхождение символа с его исходным индексом.
            .Select(g => g.First())
            // Запуск запроса, результат — List<(char Char, int Index)>.
            .ToList();

        // Множество допустимых знаков для быстрой проверки «это знак пунктуации?» (HashSet — проверка Contains за O(1)).
        var punctSet = new HashSet<char>(distinctPunctuations.Select(p => p.Char));
        // Словарь счётчиков «знак -> сколько раз встретился».
        var counts = new Dictionary<char, int>();
        // Пробегаем по уникальным знакам...
        foreach (var p in distinctPunctuations)
        {
            // ...и ставим каждому 0: так знаки, которых нет в текстах, всё равно попадут в результат с частотой 0 (тест _IncludesAllCharsEvenWithZeroFrequency).
            counts[p.Char] = 0;
        } // конец foreach (обнуление счётчиков)

        // Если files == null, просто ничего не считаем (все частоты останутся 0).
        if (files is not null)
        {
            // Последовательность ВСЕХ символов всех файлов: TryReadAllText даёт текст файла, второй SelectMany превращает строку в её символы (string — это IEnumerable<char>). Запрос ленивый — читается при foreach ниже.
            var chars = files.SelectMany(TryReadAllText).SelectMany(text => text);
            // Перебираем символы по одному (тут запрос реально выполняется).
            foreach (var ch in chars)
            {
                // Если символ — один из заданных знаков...
                if (punctSet.Contains(ch))
                {
                    // ...увеличиваем его счётчик на 1.
                    counts[ch]++;
                } // конец if (знак найден)
            } // конец foreach по символам
        } // конец if (files не null)

        // Упорядочиваем по возрастанию частоты, при равенстве — по исходному порядку в punctuations.
        // Запрос ленивый: реально выполняется в foreach ниже.
        var ordered = distinctPunctuations
            // OrderBy — по возрастанию количества вхождений знака (ключ — counts[знак]).
            .OrderBy(p => counts[p.Char])
            // ThenBy — вторичный ключ: при равной частоте — по исходной позиции знака в массиве punctuations (это даёт полностью детерминированный порядок).
            .ThenBy(p => p.Index);

        // Вставляем в словарь в отсортированном порядке (запрос ordered выполняется здесь).
        foreach (var p in ordered)
        {
            // Порядок вставки = порядок вывода в MainForm (таблица и столбцы графика слева направо — по возрастанию частоты).
            result[p.Char] = counts[p.Char];
        } // конец foreach (заполнение результата)

        // Возвращаем готовый словарь как IDictionary<char,int>.
        return result;
    } // конец метода GetPunctuationDistribution

    // Безопасно читает строки файла; при ошибке возвращает пустой набор (без исключений наружу).
    // Используется: в этом файле GetPopularCharPair (`.SelectMany(TryReadLines)`).
    // Аналог в другом решателе: NoLinqSolver.cs : TryReadLines (возвращает string[] вместо IEnumerable<string>).
    private static IEnumerable<string> TryReadLines(string path)
    {
        // try/catch — «безопасное чтение»: любая проблема с файлом (нет файла, нет доступа, занят) не должна ронять метод решателя (ТЗ: исключений наружу нет).
        try
        {
            // Читаем все строки файла; string[] неявно приводится к IEnumerable<string>.
            return File.ReadAllLines(path);
        } // конец try
        catch
        {
            // Любая ошибка -> пустая последовательность: такой файл просто «ничего не вносит». Проверяют тесты *_NonExistentFile_DoesNotThrow и AllMethods_NonExistentFolder_DoNotThrow.
            return Enumerable.Empty<string>();
        } // конец catch
    } // конец метода TryReadLines

    // Безопасно читает содержимое файла целиком; при ошибке возвращает пустой набор.
    // Используется: в этом файле GetLongWords и GetPunctuationDistribution (`.SelectMany(TryReadAllText)`) и BuildWordCounts (ниже).
    // Аналог в другом решателе: NoLinqSolver.cs : TryReadAllText (там возвращает string, а не набор).
    private static IEnumerable<string> TryReadAllText(string path)
    {
        // Та же схема безопасного чтения.
        try
        {
            // Оборачиваем текст в массив из одного элемента: тогда результат можно «сплющить» через SelectMany вместе с другими файлами.
            return new[] { File.ReadAllText(path) };
        } // конец try
        catch
        {
            // Ошибка чтения -> пустой набор (0 текстов); файл как будто отсутствует.
            return Enumerable.Empty<string>();
        } // конец catch
    } // конец метода TryReadAllText

    // Строит словарь «слово -> число вхождений» одного файла (слова в нижнем регистре).
    // Используется: в этом файле GetFileWithManyCommonWords (`files.Select(f => BuildWordCounts(f, delimiters))`).
    // Аналог в другом решателе: NoLinqSolver.cs : BuildWordCounts (то же самое, но с for вместо Select/SelectMany).
    private static Dictionary<string, int> BuildWordCounts(string file, char[] delimiters)
    {
        // Пустой словарь для накопления счётчиков.
        var dict = new Dictionary<string, int>();
        // Запрос слов файла (ленивый; выполнится в foreach ниже).
        var words = TryReadAllText(file)
            // Из текста файла получаем слова: Split по разделителям без пустых токенов.
            .SelectMany(text => text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries))
            // Приводим каждое слово к нижнему регистру.
            .Select(w => w.ToLowerInvariant());

        // Перебираем слова (запрос выполняется здесь).
        foreach (var w in words)
        {
            // Если слово уже есть — счётчик+1, иначе 1. `out int c` — выходной параметр: TryGetValue кладёт туда текущее значение (или 0).
            dict[w] = dict.TryGetValue(w, out int c) ? c + 1 : 1;
        } // конец foreach по словам

        // Возвращаем словарь; он попадает в perFileCounts (GetFileWithManyCommonWords).
        return dict;
    } // конец метода BuildWordCounts
} // конец класса LinqSolver
