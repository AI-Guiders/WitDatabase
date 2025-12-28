# Аудит классов итераторов (Iterators)

## 📊 Общая статистика

**Всего итераторов:** 16
**Базовый класс:** `IteratorBase`
**Интерфейс:** `IResultIterator`

---

## ✅ Положительные моменты

### 1. **Архитектура Volcano/Iterator Model**
- Чистая реализация pull-based итераторной модели
- Единообразный lifecycle: `Open() -> MoveNext() -> Dispose()`
- Хорошее разделение ответственности между итераторами

### 2. **Консистентность кода**
- Все итераторы следуют единому паттерну
- Правильное управление ресурсами через `IDisposable`
- Использование `#region` для структурирования кода

### 3. **Тестовое покрытие**
- Большинство итераторов имеют юнит-тесты
- Есть базовый класс `IteratorTestsBase` для тестирования

---

## ⚠️ Найденные проблемы

### 🔴 КРИТИЧЕСКИЕ

#### 1. **IteratorWindow - чрезмерная сложность**
- **985 строк кода** - самый большой класс среди итераторов
- Отвечает за **4 различные категории** функций:
  - Ranking functions (ROW_NUMBER, RANK, DENSE_RANK, NTILE, PERCENT_RANK, CUME_DIST)
  - Value functions (FIRST_VALUE, LAST_VALUE, NTH_VALUE, LAG, LEAD)
  - Aggregate window functions (COUNT, SUM, AVG, MIN, MAX)
  - Frame calculation и partition management
- Нарушает **Single Responsibility Principle**
- Сложно поддерживать и тестировать
- Высокая когнитивная нагрузка при чтении кода

**Рекомендация:** Разбить на partial классы или создать иерархию классов

#### 2. **IteratorExcept - Typo в регионе**
```csharp
#region Proeprties  // ❌ Опечатка: должно быть "Properties"
```

#### 3. **Отсутствие проверки состояния в некоторых итераторах**
- `IteratorEmpty.Current` бросает исключение, но не проверяет `IsOpen`
- Некоторые итераторы не проверяют, был ли вызван `Open()` перед `MoveNext()`

### 🟡 СРЕДНИЕ

#### 4. **Дублирование кода в set operations**
- `IteratorUnion`, `IteratorIntersect`, `IteratorExcept` имеют похожую логику для обработки `ALL`/не-`ALL` вариантов
- Можно выделить общий базовый класс или helper методы

#### 5. **IteratorInMemory - два конструктора с разной логикой**
```csharp
public IteratorInMemory(IReadOnlyList<WitSqlRow> rows, IReadOnlyList<WitSqlColumnInfo> schema)
public IteratorInMemory(IReadOnlyList<WitSqlRow> rows)
```
- Второй конструктор выводит схему из первой строки
- Если коллекция пустая, возвращает пустой массив
- Может привести к неожиданному поведению при пустых данных

#### 6. **IteratorGroupBy - дублирование константы**
```csharp
private static readonly HashSet<string> AGGREGATE_FUNCTIONS = new(StringComparer.OrdinalIgnoreCase)
{
    "COUNT", "SUM", "AVG", "MIN", "MAX", "GROUP_CONCAT"
};
```
Та же константа есть в `ExpressionEvaluator.Aggregate.cs` - надо использовать общую

#### 7. **Отсутствие валидации схем в set operations**
- `IteratorUnion`, `IteratorIntersect`, `IteratorExcept` не проверяют, что схемы левого и правого итераторов совместимы
- SQL требует, чтобы количество и типы колонок совпадали

### 🟢 МИНОРНЫЕ

#### 8. **IteratorAlias - удвоение количества колонок**
```csharp
var count = sourceNames.Count;
var names = new string[count * 2];  // Удваивает размер для aliased + original
var values = new WitSqlValue[count * 2];
```
- Это увеличивает память в 2 раза
- Возможно, стоит рассмотреть альтернативный подход (например, lazy resolution)

#### 9. **Inconsistent null handling**
- Некоторые итераторы используют `default`, другие `default(WitSqlRow)`
- Лучше использовать единообразно

#### 10. **Missing XML documentation**
- Некоторые private методы не имеют комментариев
- Особенно критично для сложных методов в `IteratorWindow`

---

## 📋 План действий

### Приоритет 1: Рефакторинг IteratorWindow

**Предлагаемое решение:** Разбить на partial классы

```
IteratorWindow.cs                      // Core + orchestration (~320 строк)
IteratorWindow.Ranking.cs              // Ranking functions (~240 строк)
IteratorWindow.Value.cs                // Value functions (~210 строк)  
IteratorWindow.Aggregate.cs            // Aggregate window functions (~220 строк)
IteratorWindow.Frame.cs                // Frame calculation (~65 строк)
IteratorWindow.Helpers.cs              // Helper methods & nested types (~150 строк)
```

**Преимущества:**
- ✅ Каждый файл < 330 строк (цель достигнута!)
- ✅ Легче найти и изменить конкретную функцию
- ✅ Проще покрыть тестами
- ✅ Меньше когнитивная нагрузка
- ✅ Сохраняется единый класс (без изменения API)

**Статус:** ✅ **ЗАВЕРШЕНО**

### Приоритет 2: Исправление критических багов
- [x] ~~Исправить опечатку `Proeprties` -> `Properties` в `IteratorExcept`~~ (уже было исправлено ранее)
- [x] Добавить проверку `IsOpen` в `IteratorEmpty`
- [x] Валидация схем в set operations (`IteratorUnion`, `IteratorIntersect`, `IteratorExcept`)

**Статус:** ✅ **ЗАВЕРШЕНО**

### Приоритет 3: Рефакторинг дублирования
- [x] Создать общий базовый класс для set operations (вынести логику валидации схем)
- [x] Вынести `AGGREGATE_FUNCTIONS` константу в общее место
- [ ] Унифицировать null handling (`default` vs `default(WitSqlRow)`)

**Статус:** 🟡 **ЧАСТИЧНО ВЫПОЛНЕНО**

**Выполнено:**
1. ✅ Создан `SetOperationSchemaValidator` - общий валидатор схем для UNION/INTERSECT/EXCEPT
2. ✅ Создан `SqlFunctions` - централизованное хранилище констант функций:
   - `SqlFunctions.Aggregates` - агрегатные функции
   - `SqlFunctions.WindowRanking` - ranking window functions  
   - `SqlFunctions.WindowValue` - value window functions
   - Методы: `IsAggregate()`, `IsWindowRanking()`, `IsWindowValue()`, `IsWindowFunction()`
3. ✅ Обновлены все классы для использования общих констант:
   - `ExpressionEvaluator.Aggregate.cs`
   - `QueryPlanner.cs` и `QueryPlanner.Helpers.cs`
   - `IteratorWindow.cs`

**Осталось:**
- Унифицировать null handling (`default` vs `default(WitSqlRow)`)

### Приорит 4: Улучшение документации
- [ ] Добавить XML комментарии для сложных методов
- [ ] Документировать assumptions и invariants

---

## 🎯 Метрики кода итераторов

| Итератор | Строки | Сложность | Зависимости | Тесты |
|----------|--------|-----------|-------------|-------|
| IteratorWindow | 985 | 🔴 Очень высокая | ExpressionEvaluator | ❌ |
| IteratorGroupBy | ~330 | 🟡 Средняя | ExpressionEvaluator, AggregateGroup | ✅ |
| IteratorJoin | ~250 | 🟡 Средняя | ExpressionEvaluator | ✅ |
| IteratorSort | ~180 | 🟢 Низкая | ExpressionEvaluator | ✅ |
| IteratorProject | ~140 | 🟢 Низкая | ExpressionEvaluator | ✅ |
| IteratorFilter | ~120 | 🟢 Низкая | ExpressionEvaluator | ✅ |
| IteratorUnion | ~120 | 🟢 Низкая | RowKey | ✅ |
| IteratorIntersect | ~120 | 🟢 Низкая | RowKey | ✅ |
| IteratorExcept | ~125 | 🟢 Низкая | RowKey | ✅ |
| IteratorDistinct | ~70 | 🟢 Низкая | RowKey | ✅ |
| IteratorLimit | ~90 | 🟢 Низкая | - | ✅ |
| IteratorInMemory | ~100 | 🟢 Низкая | - | ❓ |
| IteratorAlias | ~115 | 🟢 Низкая | - | ✅ |
| IteratorColumnRename | ~105 | 🟢 Низкая | - | ❓ |
| IteratorEmpty | ~50 | 🟢 Низкая | - | ✅ |
| IteratorSingleRow | ~60 | 🟢 Низкая | - | ✅ |

---

## 🔍 Дополнительные замечания

### Хорошие практики, которым следует код:
1. ✅ Immutable конфигурация (поля `readonly`)
2. ✅ Правильное управление ресурсами (`Dispose`)
3. ✅ Использование struct для value types (`WitSqlRow`)
4. ✅ Defensive copying где нужно
5. ✅ Использование современного C# (collection expressions, pattern matching)

### Области для улучшения:
1. ⚠️ Больше использовать `Span<T>` и `Memory<T>` для производительности
2. ⚠️ Рассмотреть async iterators для больших датасетов
3. ⚠️ Добавить cancellation tokens для длительных операций
4. ⚠️ Метрики производительности (сколько строк обработано, время выполнения)

---

## Заключение

**Общее состояние кода:** 🟢 **Хорошее**

Большинство итераторов написаны качественно и следуют best practices. Основная проблема - **IteratorWindow**, который явно нуждается в рефакторинге из-за своего размера и сложности.

**Рекомендуемые действия:**
1. 🔴 **Срочно:** Разбить `IteratorWindow` на partial классы
2. 🟡 **Важно:** Исправить найденные баги и добавить валидацию
3. 🟢 **Желательно:** Рефакторинг дублирования кода
4. 🔵 **Опционально:** Улучшение документации и метрик

