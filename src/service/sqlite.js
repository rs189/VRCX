// requires binding of SQLite

import InteropApi from '../ipc/interopApi.js';
const SQLite = InteropApi.SQLiteLegacy;

class SQLiteService {
    execute(callback, sql, args = null) {
        return new Promise((resolve, reject) => {
            if (LINUX) {
                SQLite.Execute(
                    sql,
                    new Map(args ? Object.entries(args) : [])
                ).then((data) => {
                    const status = JSON.parse(data).status;
                    data = JSON.parse(data).data;
                    if (status !== "success") {
                        reject(new Error("SQL execution failed"));
                    } else if (data === null) {
                        resolve(null);
                    } else {
                        callback(data);
                        resolve(data);
                    }
                }).catch((error) => {
                    console.error("SQL execution failed:", error);
                });
            } else {
                SQLite.Execute(
                    (err, data) => {
                        if (err !== null) {
                            reject(err);
                        } else if (data === null) {
                            resolve();
                        } else {
                            callback(data);
                        }
                    },
                    sql,
                    args
                );
            }
        });
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