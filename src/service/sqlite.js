// requires binding of SQLite

import InteropApi from '../ipc/interopApi.js';
const SQLiteDotnet = InteropApi.SQLiteLegacy;

class SQLiteService {
    execute(callback, sql, args = null) {
        return new Promise((resolve, reject) => {
            if (LINUX) {
                try {
                    let options = null;
                    if (args && typeof args === 'string') {
                        try {
                            options = JSON.parse(args);
                        } catch (err) {
                            return reject('Invalid JSON format for options');
                        }
                    } else if (args && typeof args === 'object') {
                        options = args;
                    }                

                    const data = SQLiteDotnet.Execute(sql, options);

                    if (data.rows && data.rows.length > 0) {
                        for (const row of data.rows) {
                            callback(row);
                        }
                    }

                    resolve(data);
                } catch (err) {
                    reject(err);
                }
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
            return SQLiteDotnet.ExecuteNonQuery(sql, new Map(args ? Object.entries(args) : []));
        } 
        return SQLite.ExecuteNonQuery(sql, args);
    }
}

var self = new SQLiteService();
window.sqliteService = self;

export { self as default, SQLiteService };