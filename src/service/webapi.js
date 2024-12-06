// requires binding of WebApi

const platform = navigator.platform.toLowerCase();

import InteropApi from '../ipc/interopApi.js';
const WebApi = InteropApi.WebApi;

class WebApiService {
    clearCookies() {
        WebApi.ClearCookies();
    }

    getCookies() {
        return WebApi.GetCookies();
    }

    setCookies(cookie) {
        return WebApi.SetCookies(cookie);
    }

    execute(options) {
        return new Promise((resolve, reject) => {
            if (platform.includes('linux')) {
                WebApi.ExecuteAsync(JSON.stringify(options))
                    .then(response => {
                        if (response.error) {
                            reject(response.error);
                        } else {
                            resolve(JSON.parse(response));
                        }
                    })
                    .catch(err => reject(err));
            } else {
                WebApi.Execute(options, (err, response) => {
                    if (err !== null) {
                        reject(err);
                        return;
                    }
                    resolve(response);
                });
            }
        });
    }
}

var self = new WebApiService();
window.webApiService = self;

export { self as default, WebApiService };