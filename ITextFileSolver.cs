// Пространство имён библиотеки решателей. Форма `namespace X;` (file-scoped) действует на весь файл.
// Используется: `using Lab4.Solvers;` в Lab4.UI\MainForm.cs и в Lab4.Tests\SolversTests.cs; те же namespace у LinqSolver.cs и NoLinqSolver.cs.
namespace Lab4.Solvers;

// Вариант В1.2.
// Обработка без учёта регистра.
// files - массив с txt-файлами (абс. пути).
// delimiters - разделители слов.
// Сигнатуры методов и пространство имён менять нельзя.
// Интерфейс = «контракт»: список методов без реализации. Это общий «разъём» паттерна «Стратегия»:
// форма (MainForm) знает только этот интерфейс, а какая именно реализация подставлена (LINQ или без LINQ) — ей всё равно.
// Используется: реализуют LinqSolver и NoLinqSolver; как тип переменной/параметра — MainForm.CreateSolver (возвращаемый тип),
// MainForm.ShowPopularCharPairAsync / ShowLongWordsAsync / ShowFileWithManyCommonWordsAsync / ShowPunctuationDistributionAsync (параметр solver);
// в тестах — SolversTests.Solvers() возвращает массив ITextFileSolver[].
public interface ITextFileSolver
{
    // Самая часто встречающаяся пара подряд идущих символов
    // ValueTuple<char, char> — значимый тип-кортеж из двух char (то же, что запись (char, char)); default даёт ('\0','\0') — «ничего не найдено».
    // Используется: вызывается в MainForm.ShowPopularCharPairAsync (кнопка «Выполнить», задача 1); реализации — LinqSolver.GetPopularCharPair и NoLinqSolver.GetPopularCharPair;
    // проверяют тесты GetPopularCharPair_* (SolversTests.cs) и AllMethods_NonExistentFolder_DoNotThrow.
    ValueTuple<char, char> GetPopularCharPair(string[] files);

    // N самых длинных слов
    // IList<string> — интерфейс списка строк; вызывающему не важно, List это или массив. Пустой список (не null) означает «слов нет».
    // Используется: вызывается в MainForm.ShowLongWordsAsync (задача 2); реализации — LinqSolver.GetLongWords и NoLinqSolver.GetLongWords; тесты GetLongWords_*.
    IList<string> GetLongWords(string[] files, char[] delimiters, int N);

    // Файл с наибольшим количеством общеупотребимых слов (слова, которые присутствуют
    // в каждом файле хотя бы K раз) и число таких слов
    // ValueTuple<string, int> — пара (путь к файлу, суммарное число вхождений); default = (null, 0) — «общих слов нет».
    // Используется: вызывается в MainForm.ShowFileWithManyCommonWordsAsync (задача 3); реализации — LinqSolver.GetFileWithManyCommonWords и NoLinqSolver.GetFileWithManyCommonWords; тесты GetFileWithManyCommonWords_*.
    ValueTuple<string, int> GetFileWithManyCommonWords(string[] files, char[] delimiters, int K);

    // Распределение знаков пунктуации по частоте, упорядоченность по возрастанию
    // IDictionary<char,int> — словарь «знак -> сколько раз встретился»; порядок обхода = порядок вставки, поэтому реализации вставляют по возрастанию частоты.
    // Используется: вызывается в MainForm.ShowPunctuationDistributionAsync (задача 4); реализации — LinqSolver.GetPunctuationDistribution и NoLinqSolver.GetPunctuationDistribution; тесты GetPunctuationDistribution_*.
    IDictionary<char, int> GetPunctuationDistribution(string[] files, char[] punctuations);
} // конец интерфейса ITextFileSolver
