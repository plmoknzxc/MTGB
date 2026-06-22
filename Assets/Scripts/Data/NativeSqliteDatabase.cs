using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal sealed class NativeSqliteDatabase : IDisposable
{
    private const int SqliteOk = 0;
    private const int SqliteRow = 100;
    private const int SqliteDone = 101;
    private const int OpenReadOnly = 0x00000001;

    private IntPtr handle;

    private NativeSqliteDatabase(IntPtr handle)
    {
        this.handle = handle;
    }

    public static NativeSqliteDatabase OpenReadOnlyDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Database path is required.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("Card database was not found.", path);

        int result = Native.sqlite3_open_v2(ToUtf8(path), out IntPtr database, OpenReadOnly, IntPtr.Zero);
        if (result != SqliteOk)
        {
            string message = database == IntPtr.Zero ? "Unknown SQLite open error." : GetError(database);
            if (database != IntPtr.Zero) Native.sqlite3_close_v2(database);
            throw new InvalidOperationException($"Could not open SQLite database '{path}': {message}");
        }

        return new NativeSqliteDatabase(database);
    }

    public string ExecuteScalarString(string sql)
    {
        string value = null;
        Query(sql, row => value = row.GetString(0), 1);
        return value;
    }

    public void Query(string sql, Action<Row> readRow, int maxRows = int.MaxValue)
    {
        if (handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(NativeSqliteDatabase));
        if (readRow == null) throw new ArgumentNullException(nameof(readRow));

        int result = Native.sqlite3_prepare_v2(handle, ToUtf8(sql), -1, out IntPtr statement, IntPtr.Zero);
        if (result != SqliteOk) throw new InvalidOperationException(GetError(handle));

        try
        {
            int count = 0;
            while (count < maxRows)
            {
                result = Native.sqlite3_step(statement);
                if (result == SqliteDone) break;
                if (result != SqliteRow) throw new InvalidOperationException(GetError(handle));
                readRow(new Row(statement));
                count++;
            }
        }
        finally
        {
            Native.sqlite3_finalize(statement);
        }
    }

    public void Dispose()
    {
        if (handle == IntPtr.Zero) return;
        Native.sqlite3_close_v2(handle);
        handle = IntPtr.Zero;
    }

    public readonly struct Row
    {
        private readonly IntPtr statement;

        internal Row(IntPtr statement)
        {
            this.statement = statement;
        }

        public int GetInt32(int column) => Native.sqlite3_column_int(statement, column);

        public string GetString(int column)
        {
            IntPtr value = Native.sqlite3_column_text(statement, column);
            if (value == IntPtr.Zero) return string.Empty;

            int byteCount = Native.sqlite3_column_bytes(statement, column);
            if (byteCount <= 0) return string.Empty;
            byte[] bytes = new byte[byteCount];
            Marshal.Copy(value, bytes, 0, byteCount);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static byte[] ToUtf8(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        Array.Resize(ref bytes, bytes.Length + 1);
        return bytes;
    }

    private static string GetError(IntPtr database)
    {
        IntPtr message = Native.sqlite3_errmsg(database);
        if (message == IntPtr.Zero) return "Unknown SQLite error.";

        int length = 0;
        while (Marshal.ReadByte(message, length) != 0) length++;
        byte[] bytes = new byte[length];
        Marshal.Copy(message, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static class Native
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private const string LibraryName = "winsqlite3";
#else
        private const string LibraryName = "sqlite3";
#endif

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_open_v2(byte[] filename, out IntPtr database, int flags, IntPtr vfs);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_close_v2(IntPtr database);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_prepare_v2(IntPtr database, byte[] sql, int byteCount, out IntPtr statement, IntPtr tail);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_step(IntPtr statement);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_finalize(IntPtr statement);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_int(IntPtr statement, int column);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_bytes(IntPtr statement, int column);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_errmsg(IntPtr database);
    }
}
