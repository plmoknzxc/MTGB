using System.Runtime.InteropServices;
using System.Text;

namespace MTGB.CardDatabaseEditor;

internal sealed class NativeSqlite : IDisposable
{
    private const int Ok = 0;
    private const int Row = 100;
    private const int Done = 101;
    private const int OpenReadWrite = 0x00000002;
    private const int OpenCreate = 0x00000004;
    private static readonly IntPtr Transient = new(-1);

    private IntPtr database;

    private NativeSqlite(IntPtr database)
    {
        this.database = database;
        Execute("PRAGMA foreign_keys = ON;");
        Native.sqlite3_busy_timeout(database, 3000);
    }

    public static NativeSqlite Open(string path, bool create)
    {
        int flags = OpenReadWrite | (create ? OpenCreate : 0);
        int result = Native.sqlite3_open_v2(Utf8(path), out IntPtr handle, flags, IntPtr.Zero);
        if (result != Ok)
        {
            string message = handle == IntPtr.Zero ? "Unknown SQLite open error." : Error(handle);
            if (handle != IntPtr.Zero) Native.sqlite3_close_v2(handle);
            throw new InvalidOperationException(message);
        }
        return new NativeSqlite(handle);
    }

    public void Execute(string sql)
    {
        EnsureOpen();
        int result = Native.sqlite3_exec(database, Utf8(sql), IntPtr.Zero, IntPtr.Zero, out IntPtr error);
        if (result == Ok) return;

        string message = error == IntPtr.Zero ? Error(database) : PtrToString(error);
        if (error != IntPtr.Zero) Native.sqlite3_free(error);
        throw new InvalidOperationException(message);
    }

    public int ExecuteNonQuery(string sql, params object?[] parameters)
    {
        EnsureOpen();
        IntPtr statement = Prepare(sql);
        try
        {
            Bind(statement, parameters);
            int result = Native.sqlite3_step(statement);
            if (result != Done) throw new InvalidOperationException(Error(database));
            return Native.sqlite3_changes(database);
        }
        finally
        {
            Native.sqlite3_finalize(statement);
        }
    }

    public void Query(string sql, Action<SqliteRow> readRow, params object?[] parameters)
    {
        EnsureOpen();
        IntPtr statement = Prepare(sql);
        try
        {
            Bind(statement, parameters);
            while (true)
            {
                int result = Native.sqlite3_step(statement);
                if (result == Done) break;
                if (result != Row) throw new InvalidOperationException(Error(database));
                readRow(new SqliteRow(statement));
            }
        }
        finally
        {
            Native.sqlite3_finalize(statement);
        }
    }

    public string? ScalarString(string sql, params object?[] parameters)
    {
        string? value = null;
        Query(sql, row => value ??= row.String(0), parameters);
        return value;
    }

    public int ScalarInt(string sql, params object?[] parameters)
    {
        int value = 0;
        bool found = false;
        Query(sql, row =>
        {
            if (found) return;
            value = row.Int32(0);
            found = true;
        }, parameters);
        return value;
    }

    public void Dispose()
    {
        if (database == IntPtr.Zero) return;
        Native.sqlite3_close_v2(database);
        database = IntPtr.Zero;
    }

    private IntPtr Prepare(string sql)
    {
        int result = Native.sqlite3_prepare_v2(database, Utf8(sql), -1, out IntPtr statement, IntPtr.Zero);
        if (result != Ok) throw new InvalidOperationException(Error(database));
        return statement;
    }

    private void Bind(IntPtr statement, object?[] parameters)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            int index = i + 1;
            object? value = parameters[i];
            int result = value switch
            {
                null => Native.sqlite3_bind_null(statement, index),
                bool boolean => Native.sqlite3_bind_int(statement, index, boolean ? 1 : 0),
                byte number => Native.sqlite3_bind_int(statement, index, number),
                short number => Native.sqlite3_bind_int(statement, index, number),
                int number => Native.sqlite3_bind_int(statement, index, number),
                long number => Native.sqlite3_bind_int64(statement, index, number),
                Enum enumValue => Native.sqlite3_bind_int64(statement, index, Convert.ToInt64(enumValue)),
                _ => BindText(statement, index, Convert.ToString(value) ?? string.Empty)
            };
            if (result != Ok) throw new InvalidOperationException(Error(database));
        }
    }

    private static int BindText(IntPtr statement, int index, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Native.sqlite3_bind_text(statement, index, bytes, bytes.Length, Transient);
    }

    private void EnsureOpen()
    {
        if (database == IntPtr.Zero) throw new ObjectDisposedException(nameof(NativeSqlite));
    }

    private static byte[] Utf8(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Array.Resize(ref bytes, bytes.Length + 1);
        return bytes;
    }

    private static string Error(IntPtr handle) => PtrToString(Native.sqlite3_errmsg(handle));

    private static string PtrToString(IntPtr value)
    {
        if (value == IntPtr.Zero) return string.Empty;
        int length = 0;
        while (Marshal.ReadByte(value, length) != 0) length++;
        byte[] bytes = new byte[length];
        Marshal.Copy(value, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    internal readonly struct SqliteRow
    {
        private readonly IntPtr statement;

        internal SqliteRow(IntPtr statement) => this.statement = statement;

        public int Int32(int column) => Native.sqlite3_column_int(statement, column);

        public string String(int column)
        {
            IntPtr value = Native.sqlite3_column_text(statement, column);
            if (value == IntPtr.Zero) return string.Empty;
            int length = Native.sqlite3_column_bytes(statement, column);
            byte[] bytes = new byte[length];
            Marshal.Copy(value, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static class Native
    {
        private const string Library = "winsqlite3";

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_open_v2(byte[] filename, out IntPtr database, int flags, IntPtr vfs);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_close_v2(IntPtr database);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_exec(IntPtr database, byte[] sql, IntPtr callback, IntPtr callbackArg, out IntPtr errorMessage);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sqlite3_free(IntPtr value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_prepare_v2(IntPtr database, byte[] sql, int byteCount, out IntPtr statement, IntPtr tail);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_step(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_null(IntPtr statement, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_int(IntPtr statement, int index, int value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int byteCount, IntPtr destructor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_int(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_bytes(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_changes(IntPtr database);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_errmsg(IntPtr database);
    }
}
