// requires binding of WebApi

import InteropApi from '../ipc/interopApi.js';
const WebApiDotnet = InteropApi.WebApi;

class WebApiService {
    clearCookies() {
        return WebApiDotnet ? WebApiDotnet.ClearCookies() : WebApi.ClearCookies();
    }

    getCookies() {
        return WebApiDotnet ? WebApiDotnet.GetCookies() : WebApi.GetCookies();
    }

    setCookies(cookie) {
        return WebApiDotnet ? WebApiDotnet.SetCookies(cookie) : WebApi.SetCookies(cookie);
    }

    execute(options) {
        return new Promise((resolve, reject) => {
            if (LINUX) {
                WebApiDotnet.ExecuteAsync(JSON.stringify(options))
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