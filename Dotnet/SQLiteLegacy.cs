#if LINUX
#else
using CefSharp;
#endif
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.JavaScript.NodeApi;

namespace VRCX
{
    public class SQLiteLegacy
    {
#if LINUX
        public static SQLiteLegacy Instance;
#else
        public static readonly SQLiteLegacy Instance;
#endif
        private readonly ReaderWriterLockSlim m_ConnectionLock;
        private SQLiteConnection m_Connection;

        static SQLiteLegacy()
        {
            Instance = new SQLiteLegacy();
        }

        public SQLiteLegacy()
        {
            m_ConnectionLock = new ReaderWriterLockSlim();
        }

#if LINUX
        public void Init()
        {
            Instance = this;

            var dataSource = Program.ConfigLocation;
            var jsonDataSource = VRCXStorage.Instance.Get("VRCX_DatabaseLocation");
            if (!string.IsNullOrEmpty(jsonDataSource))
                dataSource = jsonDataSource;

            m_Connection = new SQLiteConnection($"Data Source=\"{dataSource}\";Version=3;PRAGMA locking_mode=NORMAL;PRAGMA busy_timeout=5000;PRAGMA journal_mode=WAL;", true);

            m_Connection.Open();
        }
#else
        internal void Init()
        {
            var dataSource = Program.ConfigLocation;
            var jsonDataSource = VRCXStorage.Instance.Get("VRCX_DatabaseLocation");
            if (!string.IsNullOrEmpty(jsonDataSource))
                dataSource = jsonDataSource;

            m_Connection = new SQLiteConnection($"Data Source=\"{dataSource}\";Version=3;PRAGMA locking_mode=NORMAL;PRAGMA busy_timeout=5000;PRAGMA journal_mode=WAL;", true);

            m_Connection.Open();
        }
#endif
        internal void Exit()
        {
            m_Connection.Close();
            m_Connection.Dispose();
        }
#if LINUX  
        public async Task<object[]> ExecuteTest(string sql, IDictionary<string, object> args = null)
        {
            Console.WriteLine("SQLiteLegacy.ExecuteTest: " + sql);

            return await Task.Run(() =>
            {
                Console.WriteLine("SQLiteLegacy.ExecuteTest: Task.Run");

                m_ConnectionLock.EnterReadLock();
                try
                {
                    Console.WriteLine("SQLiteLegacy.ExecuteTest: Try Task.Run");

                    using (var command = new SQLiteCommand(sql, m_Connection))
                    {
                        Console.WriteLine("SQLiteLegacy.ExecuteTest: new SQLiteCommand");

                        if (args != null)
                        {
                            foreach (var arg in args)
                            {
                                command.Parameters.Add(new SQLiteParameter(arg.Key, arg.Value));
                            }
                        }
                        using (var reader = command.ExecuteReader())
                        {
                            Console.WriteLine("SQLiteLegacy.ExecuteTest: command.ExecuteReader()");

                            if (reader.HasRows)
                            {
                                Console.WriteLine("SQLiteLegacy.ExecuteTest: reader has rows");

                                while (reader.Read())
                                {
                                    Console.WriteLine("SQLiteLegacy.ExecuteTest: reader.Read() == true");
                                    var values = new object[reader.FieldCount];
                                    reader.GetValues(values);
                                    return values;
                                }
                            }
                            else
                            {
                                Console.WriteLine("SQLiteLegacy.ExecuteTest: No rows found");
                            }
                        }
                    }
                }
                finally
                {
                    Console.WriteLine("SQLiteLegacy.ExecuteTest: finally");

                    m_ConnectionLock.ExitReadLock();
                }

                Console.WriteLine("SQLiteLegacy.ExecuteTest: null");

                return null;
            });
        }
#else
        public void Execute(IJavascriptCallback callback, string sql, IDictionary<string, object> args = null)
        {
            try
            {
                m_ConnectionLock.EnterReadLock();
                try
                {
                    using (var command = new SQLiteCommand(sql, m_Connection))
                    {
                        if (args != null)
                        {
                            foreach (var arg in args)
                            {
                                command.Parameters.Add(new SQLiteParameter(arg.Key, arg.Value));
                            }
                        }
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read() == true)
                            {
                                var values = new object[reader.FieldCount];
                                reader.GetValues(values);
                                if (callback.CanExecute == true)
                                {
                                    callback.ExecuteAsync(null, values);
                                }
                            }
                        }
                    }
                    if (callback.CanExecute == true)
                    {
                        callback.ExecuteAsync(null, null);
                    }
                }
                finally
                {
                    m_ConnectionLock.ExitReadLock();
                }
            }
            catch (Exception e)
            {
                if (callback.CanExecute == true)
                {
                    callback.ExecuteAsync(e.Message, null);
                }
            }

            callback.Dispose();
        }
#endif
        public void Execute(Action<object[]> callback, string sql, IDictionary<string, object> args = null)
        {
            m_ConnectionLock.EnterReadLock();
            try
            {
                using (var command = new SQLiteCommand(sql, m_Connection))
                {
                    if (args != null)
                    {
                        foreach (var arg in args)
                        {
                            command.Parameters.Add(new SQLiteParameter(arg.Key, arg.Value));
                        }
                    }
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read() == true)
                        {
                            var values = new object[reader.FieldCount];
                            reader.GetValues(values);
                            callback(values);
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                m_ConnectionLock.ExitReadLock();
            }
        }

        public int ExecuteNonQuery(string sql, IDictionary<string, object> args = null)
        {
            int result = -1;

            m_ConnectionLock.EnterWriteLock();
            try
            {
                using (var command = new SQLiteCommand(sql, m_Connection))
                {
                    if (args != null)
                    {
                        foreach (var arg in args)
                        {
                            command.Parameters.Add(new SQLiteParameter(arg.Key, arg.Value));
                        }
                    }
                    result = command.ExecuteNonQuery();
                }
            }
            finally
            {
                m_ConnectionLock.ExitWriteLock();
            }

            return result;
        }
    }
}
