/**
 * The preload script runs before `index.html` is loaded
 * in the renderer. It has access to web APIs as well as
 * Electron's renderer process modules and some polyfilled
 * Node.js functions.
 *
 * https://www.electronjs.org/docs/latest/tutorial/sandbox
 */
const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("interopApi", {
    callDotNetMethod: (className, methodName, args) => {
        return ipcRenderer.invoke("callDotNetMethod", className, methodName, args);
    }
});

contextBridge.exposeInMainWorld('electron', {
    openFileDialog: () => ipcRenderer.invoke('dialog:openFile')
});