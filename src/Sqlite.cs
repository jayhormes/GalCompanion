using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GalCompanion
{
    /// <summary>
    /// 読み取り専用の最小 SQLite。Windows 10 以降が持っている winsqlite3.dll を直接叩くので、
    /// ネイティブ DLL を同梱しなくていい。
    /// </summary>
    internal sealed class Sqlite : IDisposable
    {
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_OPEN_READONLY = 0x00000001;

        private const string Dll = "winsqlite3.dll";
        private const CallingConvention Cdecl = CallingConvention.Cdecl;

        [DllImport(Dll, EntryPoint = "sqlite3_open_v2", CallingConvention = Cdecl)]
        private static extern int Open(byte[] filename, out IntPtr db, int flags, IntPtr vfs);

        [DllImport(Dll, EntryPoint = "sqlite3_close_v2", CallingConvention = Cdecl)]
        private static extern int Close(IntPtr db);

        [DllImport(Dll, EntryPoint = "sqlite3_prepare_v2", CallingConvention = Cdecl)]
        private static extern int Prepare(IntPtr db, byte[] sql, int nByte, out IntPtr stmt, out IntPtr tail);

        [DllImport(Dll, EntryPoint = "sqlite3_step", CallingConvention = Cdecl)]
        private static extern int Step(IntPtr stmt);

        [DllImport(Dll, EntryPoint = "sqlite3_finalize", CallingConvention = Cdecl)]
        private static extern int Finalize(IntPtr stmt);

        [DllImport(Dll, EntryPoint = "sqlite3_column_count", CallingConvention = Cdecl)]
        private static extern int ColumnCount(IntPtr stmt);

        [DllImport(Dll, EntryPoint = "sqlite3_column_type", CallingConvention = Cdecl)]
        private static extern int ColumnType(IntPtr stmt, int col);

        [DllImport(Dll, EntryPoint = "sqlite3_column_double", CallingConvention = Cdecl)]
        private static extern double ColumnDouble(IntPtr stmt, int col);

        [DllImport(Dll, EntryPoint = "sqlite3_column_int64", CallingConvention = Cdecl)]
        private static extern long ColumnInt64(IntPtr stmt, int col);

        [DllImport(Dll, EntryPoint = "sqlite3_column_text", CallingConvention = Cdecl)]
        private static extern IntPtr ColumnText(IntPtr stmt, int col);

        [DllImport(Dll, EntryPoint = "sqlite3_column_bytes", CallingConvention = Cdecl)]
        private static extern int ColumnBytes(IntPtr stmt, int col);

        [DllImport(Dll, EntryPoint = "sqlite3_errmsg", CallingConvention = Cdecl)]
        private static extern IntPtr ErrMsg(IntPtr db);

        private IntPtr db;

        public Sqlite(string path)
        {
            var rc = Open(Utf8(path), out db, SQLITE_OPEN_READONLY, IntPtr.Zero);
            if (rc != SQLITE_OK)
            {
                var message = db == IntPtr.Zero ? "rc=" + rc : ReadUtf8(ErrMsg(db));
                if (db != IntPtr.Zero)
                {
                    Close(db);
                    db = IntPtr.Zero;
                }
                throw new InvalidOperationException($"開けませんでした：{path}（{message}）");
            }
        }

        /// <summary>全行をそのまま返す。行数はゲーム数ぶんなので一括で問題ない。</summary>
        public List<object[]> Query(string sql)
        {
            IntPtr stmt, tail;
            var rc = Prepare(db, Utf8(sql), -1, out stmt, out tail);
            if (rc != SQLITE_OK)
            {
                throw new InvalidOperationException($"SQL 失敗：{ReadUtf8(ErrMsg(db))}");
            }

            var rows = new List<object[]>();
            try
            {
                var columns = ColumnCount(stmt);
                while (true)
                {
                    rc = Step(stmt);
                    if (rc == SQLITE_DONE)
                    {
                        break;
                    }
                    if (rc != SQLITE_ROW)
                    {
                        throw new InvalidOperationException($"読み取り失敗：{ReadUtf8(ErrMsg(db))}");
                    }

                    var row = new object[columns];
                    for (var i = 0; i < columns; i++)
                    {
                        switch (ColumnType(stmt, i))
                        {
                            case 1: row[i] = ColumnInt64(stmt, i); break;   // INTEGER
                            case 2: row[i] = ColumnDouble(stmt, i); break;  // FLOAT
                            case 5: row[i] = null; break;                   // NULL
                            default: row[i] = ReadUtf8(ColumnText(stmt, i), ColumnBytes(stmt, i)); break;
                        }
                    }
                    rows.Add(row);
                }
            }
            finally
            {
                Finalize(stmt);
            }
            return rows;
        }

        private static byte[] Utf8(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
            var terminated = new byte[bytes.Length + 1];
            Array.Copy(bytes, terminated, bytes.Length);
            return terminated;
        }

        private static string ReadUtf8(IntPtr ptr, int length = -1)
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }
            if (length < 0)
            {
                length = 0;
                while (Marshal.ReadByte(ptr, length) != 0)
                {
                    length++;
                }
            }
            var buffer = new byte[length];
            Marshal.Copy(ptr, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        public void Dispose()
        {
            if (db != IntPtr.Zero)
            {
                Close(db);
                db = IntPtr.Zero;
            }
        }
    }
}
