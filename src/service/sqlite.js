// requires binding of SQLite

import InteropApi from '../ipc/interopApi.js';
if (LINUX) {
    var SQLite = InteropApi.SQLiteLegacy;
}

class SQLiteService {
    async execute(callback, sql, args = null) {
        if (LINUX) {
            const json = await SQLite.ExecuteJson(
                sql,
                new Map(args ? Object.entries(args) : []));
            const data = JSON.parse(json);
            if (data.status !== "success") {
                throw data.data;
            }
            data.data?.forEach((item) => {
                callback(item);
            });
        } else {
            var item = await SQLite.Execute(sql, args);
            if (item.Item1 !== null) {
                throw item.Item1;
            }
            item.Item2?.forEach((item) => {
                callback(item);
            });
        }
    }

    executeNonQuery(sql, args = null) {
        if (LINUX) {
            return SQLite.ExecuteNonQuery(sql, new Map(args ? Object.entries(args) : []));
        }
        return SQLite.ExecuteNonQuery(sql, args);
    }
}

var self = new SQLiteService();
window.sqliteService = self;

export { self as default, SQLiteService };
