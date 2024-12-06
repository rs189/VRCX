#if LINUX
using Microsoft.JavaScript.NodeApi;
using Newtonsoft.Json;
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
        public string Execute(string sql, string jsonOptions = null)
        {
            try
            {
                m_ConnectionLock.EnterReadLock();
                try
                {
                    IDictionary<string, object> options = null;

                    if (!string.IsNullOrEmpty(jsonOptions))
                    {
                        try
                        {
                            options = JsonConvert.DeserializeObject<IDictionary<string, object>>(jsonOptions);
                        }
                        catch (JsonException ex)
                        {
                            return JsonConvert.SerializeObject(new
                            {
                                status = "error",
                                message = "Invalid JSON format for options",
                                error = ex.Message
                            });
                        }
                    }

                    using (var command = new SQLiteCommand(sql, m_Connection))
                    {
                        if (options != null)
                        {
                            foreach (var arg in options)
                            {
                                command.Parameters.Add(new SQLiteParameter(arg.Key, arg.Value));
                            }
                        }

                        var results = new List<Dictionary<string, object>>();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var row = new Dictionary<string, object>();
                                for (var i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.GetValue(i);
                                }
                                results.Add(row);
                            }
                        }

                        return JsonConvert.SerializeObject(new
                        {
                            status = "success",
                            rows = results
                        });
                    }
                }
                finally
                {
                    m_ConnectionLock.ExitReadLock();
                }
            }
            catch (Exception e)
            {
                return JsonConvert.SerializeObject(new
                {
                    status = "error",
                    message = e.Message
                });
            }
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
