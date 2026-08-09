using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

/// <summary>
/// Emuera EM 확장의 <c>DT_*</c> (DataTable) 저장소.
///
/// 규격: https://gitlab.com/EvilMask/emuera.em.doc
/// EM 문서가 <c>DT_SELECT</c> 를 「<c>System.Data.DataTable.Select</c> 그대로」로
/// 명시하므로 BCL 의 DataTable 을 그대로 감싼다. 필터식(<c>"age &gt;= 18"</c>)과
/// 정렬식(<c>"age ASC, height DESC"</c>) 문법이 BCL 에서 따라온다.
///
/// <para><b>주의 — Android 미검증.</b> DataTable 의 필터식 파서는 리플렉션에
/// 의존한다. Android 내보내기에서 트리밍이 걸리면 런타임에 깨질 수 있다.
/// 데스크톱/CI(Linux)에서 통과하는 것만으로 판단해서는 안 된다.</para>
///
/// 수명: EM 규격대로 RESETDATA 와 타이틀 복귀에서 삭제된다.
/// 스레드: 엔진 스레드에서만 접근한다.
/// </summary>
internal static class EmDataTableStore
{
    /// <summary>DataTable 생성 직후 자동으로 붙는 열 이름(EM 규격).</summary>
    internal const string IdColumn = "id";

    static readonly Dictionary<string, DataTable> tables = new(StringComparer.Ordinal);
    /// <summary>이름별 다음 id. DataTable 의 AutoIncrement 대신 직접 관리한다.</summary>
    static readonly Dictionary<string, long> nextId = new(StringComparer.Ordinal);

    // ------------------------------------------------------------------
    // 관리
    // ------------------------------------------------------------------

    internal static long Create(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        if (tables.ContainsKey(name)) return 0;
        var t = new DataTable(name) { CaseSensitive = true };
        // id 열은 생성 직후 자동으로 추가된다(EM 규격).
        t.Columns.Add(IdColumn, typeof(long));
        tables[name] = t;
        nextId[name] = 0;
        return 1;
    }

    internal static long Exists(string name)
        => name != null && tables.ContainsKey(name) ? 1 : 0;

    internal static long Release(string name)
    {
        if (name != null)
        {
            if (tables.TryGetValue(name, out var t)) t.Dispose();
            tables.Remove(name);
            nextId.Remove(name);
        }
        return 1;
    }

    internal static long Clear(string name)
    {
        if (!TryGet(name, out var t)) return -1;
        t.Rows.Clear();
        return 1;
    }

    /// <summary>DT_SELECT 의 문자열 비교에서 대소문자를 무시할지.</summary>
    internal static long NoCase(string name, long ignoreCase)
    {
        if (!TryGet(name, out var t)) return -1;
        t.CaseSensitive = ignoreCase == 0;
        return 1;
    }

    // ------------------------------------------------------------------
    // 열
    // ------------------------------------------------------------------

    /// <summary>EM 의 타입 번호(1~5) / 타입 이름을 CLR 타입으로.</summary>
    static Type TypeOf(string? typeName, long typeNo)
    {
        if (!string.IsNullOrEmpty(typeName))
        {
            switch (typeName!.ToLowerInvariant())
            {
                case "int8": return typeof(sbyte);
                case "int16": return typeof(short);
                case "int32": return typeof(int);
                case "int64": return typeof(long);
                case "string": return typeof(string);
            }
        }
        return typeNo switch
        {
            1 => typeof(sbyte),
            2 => typeof(short),
            3 => typeof(int),
            4 => typeof(long),
            _ => typeof(string),   // 5 또는 미지정이 기본
        };
    }

    /// <summary>CLR 타입을 EM 의 타입 번호로. DT_COLUMN_EXIST 가 이 값을 돌려준다.</summary>
    static long TypeNoOf(Type t)
    {
        if (t == typeof(sbyte)) return 1;
        if (t == typeof(short)) return 2;
        if (t == typeof(int)) return 3;
        if (t == typeof(long)) return 4;
        return 5;
    }

    internal static long ColumnAdd(string name, string column, string? typeName, long typeNo, long nullable)
    {
        if (!TryGet(name, out var t)) return -1;
        if (string.IsNullOrEmpty(column)) return 0;
        if (t.Columns.Contains(column)) return 0;
        var col = new DataColumn(column, TypeOf(typeName, typeNo))
        {
            AllowDBNull = nullable != 0,
        };
        t.Columns.Add(col);
        return 1;
    }

    /// <summary>있으면 타입 번호, 없으면 0.</summary>
    internal static long ColumnExist(string name, string column)
    {
        if (!TryGet(name, out var t)) return 0;
        if (string.IsNullOrEmpty(column) || !t.Columns.Contains(column)) return 0;
        return TypeNoOf(t.Columns[column]!.DataType);
    }

    internal static long ColumnRemove(string name, string column)
    {
        if (!TryGet(name, out var t)) return -1;
        // id 열은 지울 수 없다.
        if (string.IsNullOrEmpty(column)
            || string.Equals(column, IdColumn, StringComparison.Ordinal)
            || !t.Columns.Contains(column))
            return 0;
        t.Columns.Remove(column);
        return 1;
    }

    internal static long ColumnLength(string name)
        => TryGet(name, out var t) ? t.Columns.Count : -1;

    internal static List<string>? ColumnNames(string name)
    {
        if (!TryGet(name, out var t)) return null;
        var list = new List<string>(t.Columns.Count);
        foreach (DataColumn c in t.Columns) list.Add(c.ColumnName);
        return list;
    }

    /// <summary>현재는 DEFAULT 옵션만 지원한다(EM 문서도 DEFAULT 만 정의).</summary>
    internal static long ColumnOption(string name, string column, string option, string value)
    {
        if (!TryGet(name, out var t)) return -1;
        if (string.IsNullOrEmpty(column) || !t.Columns.Contains(column)) return 0;
        if (!string.Equals(option, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            return 0;
        var col = t.Columns[column]!;
        try
        {
            col.DefaultValue = Convert.ChangeType(value, col.DataType, CultureInfo.InvariantCulture);
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    // ------------------------------------------------------------------
    // 행
    // ------------------------------------------------------------------

    /// <summary>행을 추가하고 그 행의 id 를 돌려준다.</summary>
    internal static long RowAdd(string name, IReadOnlyList<KeyValuePair<string, string>> values)
    {
        if (!TryGet(name, out var t)) return -1;
        var row = t.NewRow();
        long id = nextId.TryGetValue(name, out var n) ? n : 0;
        nextId[name] = id + 1;
        row[IdColumn] = id;
        foreach (var kv in values)
            AssignCell(t, row, kv.Key, kv.Value);
        t.Rows.Add(row);
        return id;
    }

    /// <summary>id 로 찾은 행의 값을 고친다.</summary>
    internal static long RowSet(string name, long idValue,
        IReadOnlyList<KeyValuePair<string, string>> values)
    {
        if (!TryGet(name, out var t)) return -1;
        var row = FindById(t, idValue);
        if (row == null) return 0;
        foreach (var kv in values)
        {
            // id 는 편집할 수 없다(EM 규격).
            if (string.Equals(kv.Key, IdColumn, StringComparison.Ordinal)) continue;
            AssignCell(t, row, kv.Key, kv.Value);
        }
        return 1;
    }

    internal static long RowRemove(string name, long idValue)
    {
        if (!TryGet(name, out var t)) return -1;
        var row = FindById(t, idValue);
        if (row == null) return 0;
        t.Rows.Remove(row);
        return 1;
    }

    internal static long RowLength(string name)
        => TryGet(name, out var t) ? t.Rows.Count : -1;

    // ------------------------------------------------------------------
    // 셀
    // ------------------------------------------------------------------

    /// <summary>실패 시 0.</summary>
    internal static long CellGetInt(string name, long row, string column, long asId)
    {
        var cell = Cell(name, row, column, asId);
        if (cell == null || cell == DBNull.Value) return 0;
        try { return Convert.ToInt64(cell, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    /// <summary>실패 시 빈 문자열.</summary>
    internal static string CellGetStr(string name, long row, string column, long asId)
    {
        var cell = Cell(name, row, column, asId);
        if (cell == null || cell == DBNull.Value) return "";
        return Convert.ToString(cell, CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>null 이면 1, 아니면 0. 행이나 열이 없으면 -2.</summary>
    internal static long CellIsNull(string name, long row, string column, long asId)
    {
        if (!TryGet(name, out var t)) return -1;
        var r = Row(t, row, asId);
        if (r == null || string.IsNullOrEmpty(column) || !t.Columns.Contains(column))
            return -2;
        return r[column] == DBNull.Value ? 1 : 0;
    }

    /// <summary>행이나 열이 없으면 -3.</summary>
    internal static long CellSet(string name, long row, string column, string? value, long asId)
    {
        if (!TryGet(name, out var t)) return -1;
        var r = Row(t, row, asId);
        if (r == null || string.IsNullOrEmpty(column) || !t.Columns.Contains(column))
            return -3;
        if (string.Equals(column, IdColumn, StringComparison.Ordinal)) return 0;
        return AssignCell(t, r, column, value) ? 1 : 0;
    }

    // ------------------------------------------------------------------
    // 조회
    // ------------------------------------------------------------------

    /// <summary>
    /// 조건에 맞는 행의 id 목록. 실패하면 null.
    /// 필터·정렬 문법은 System.Data.DataTable.Select 그대로다.
    /// </summary>
    internal static List<long>? Select(string name, string? filter, string? sort)
    {
        if (!TryGet(name, out var t)) return null;
        DataRow[] rows;
        try
        {
            rows = t.Select(string.IsNullOrEmpty(filter) ? null : filter,
                            string.IsNullOrEmpty(sort) ? null : sort);
        }
        catch (Exception e)
        {
            // 잘못된 필터식은 게임 쪽 실수다. 엔진을 죽이지 않고 알린다.
            uEmuera.Logger.Warn($"DT_SELECT('{name}', '{filter}', '{sort}'): {e.Message}");
            return null;
        }
        var ids = new List<long>(rows.Length);
        foreach (var r in rows)
            ids.Add(Convert.ToInt64(r[IdColumn], CultureInfo.InvariantCulture));
        return ids;
    }

    // ------------------------------------------------------------------
    // 내부
    // ------------------------------------------------------------------

    static bool TryGet(string? name, out DataTable table)
    {
        if (name != null && tables.TryGetValue(name, out var t))
        {
            table = t;
            return true;
        }
        table = null!;
        return false;
    }

    static DataRow? FindById(DataTable t, long id)
    {
        foreach (DataRow r in t.Rows)
        {
            if (r.RowState == DataRowState.Deleted) continue;
            if (r[IdColumn] != DBNull.Value
                && Convert.ToInt64(r[IdColumn], CultureInfo.InvariantCulture) == id)
                return r;
        }
        return null;
    }

    /// <summary>asId 가 1 이면 id 로, 그 외에는 0 기준 순번으로 행을 찾는다.</summary>
    static DataRow? Row(DataTable t, long row, long asId)
    {
        if (asId == 1) return FindById(t, row);
        if (row < 0 || row >= t.Rows.Count) return null;
        return t.Rows[(int)row];
    }

    static object? Cell(string name, long row, string column, long asId)
    {
        if (!TryGet(name, out var t)) return null;
        var r = Row(t, row, asId);
        if (r == null || string.IsNullOrEmpty(column) || !t.Columns.Contains(column))
            return null;
        return r[column];
    }

    /// <summary>문자열로 받은 값을 열 타입에 맞춰 넣는다. value 가 null 이면 DBNull.</summary>
    static bool AssignCell(DataTable t, DataRow row, string column, string? value)
    {
        if (string.IsNullOrEmpty(column) || !t.Columns.Contains(column))
            return false;
        var col = t.Columns[column]!;
        if (value == null)
        {
            if (!col.AllowDBNull) return false;
            row[column] = DBNull.Value;
            return true;
        }
        try
        {
            if (col.DataType == typeof(string))
                row[column] = value;
            else
                row[column] = Convert.ChangeType(value, col.DataType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// DT_TOXML: 스키마 XML 과 데이터 XML 을 만든다.
    /// 반환값이 데이터 XML, out 이 스키마 XML. 테이블이 없으면 둘 다 "".
    ///
    /// DataTable.WriteXmlSchema / WriteXml 를 그대로 쓴다. 규격이 이 클래스
    /// 기반이라고 명시하므로, 직접 만든 형식보다 PC판과 호환될 가능성이 높다.
    /// </summary>
    internal static string ToXml(string name, out string schema)
    {
        schema = "";
        if (!TryGet(name, out var t))
            return "";
        try
        {
            using (var sw = new System.IO.StringWriter())
            {
                t.WriteXmlSchema(sw);
                schema = sw.ToString();
            }
            using (var sw = new System.IO.StringWriter())
            {
                t.WriteXml(sw, XmlWriteMode.IgnoreSchema);
                return sw.ToString();
            }
        }
        catch
        {
            schema = "";
            return "";
        }
    }

    /// <summary>
    /// DT_FROMXML: 스키마와 데이터 XML 로 테이블을 덮어쓴다. 성공 1, 실패 0.
    ///
    /// 실패했을 때 원래 테이블을 반쯤 망가진 상태로 남기지 않도록, 새 테이블에
    /// 먼저 읽어들여 성공한 뒤에 교체한다.
    /// </summary>
    internal static long FromXml(string name, string schemaXml, string dataXml)
    {
        if (string.IsNullOrWhiteSpace(name))
            return 0;
        if (!tables.ContainsKey(name))
            return 0;
        DataTable? loaded = null;
        try
        {
            loaded = new DataTable();
            if (!string.IsNullOrWhiteSpace(schemaXml))
                using (var sr = new System.IO.StringReader(schemaXml))
                    loaded.ReadXmlSchema(sr);
            if (!string.IsNullOrWhiteSpace(dataXml))
                using (var sr = new System.IO.StringReader(dataXml))
                    loaded.ReadXml(sr);
        }
        catch
        {
            loaded?.Dispose();
            return 0;
        }

        var old = tables[name];
        tables[name] = loaded;
        old.Dispose();
        // id 자동 증가 카운터를 실제 데이터에 맞춘다. 맞추지 않으면 다음
        // DT_ROW_ADD 가 이미 있는 id 를 다시 쓴다.
        nextId[name] = NextIdFrom(loaded);
        return 1;
    }

    /// <summary>불러온 테이블의 id 열 최대값 + 1. 없으면 0.</summary>
    static long NextIdFrom(DataTable t)
    {
        if (!t.Columns.Contains(IdColumn))
            return 0;
        long max = -1;
        foreach (DataRow r in t.Rows)
        {
            var v = r[IdColumn];
            if (v == null || v == DBNull.Value)
                continue;
            if (long.TryParse(Convert.ToString(v), out var n) && n > max)
                max = n;
        }
        return max + 1;
    }

    /// <summary>타이틀 복귀 / RESETDATA 시 호출.</summary>
    internal static void ClearAll()
    {
        foreach (var t in tables.Values) t.Dispose();
        tables.Clear();
        nextId.Clear();
    }
}
