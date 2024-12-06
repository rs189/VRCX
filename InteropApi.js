const path = require('path');
const dotnet = require('node-api-dotnet');
//require('./build/bin/AnyCPU/Debug/VRCX.cjs');
const VRCXPath = path.join(__dirname, './build/bin/AnyCPU/Debug/VRCX.cjs');

class InteropApi {
    constructor() {
        // Cache for .NET objects, might be problematic if we require a new instance every time
        this.createdObjects = {};
    }

    getDotNetObject(className) {
        if (!this.createdObjects[className]) {
            this.createdObjects[className] = new dotnet.VRCX[className]();
        }
        return this.createdObjects[className];
    }

    callMethod(className, methodName, args) {
        const obj = this.getDotNetObject(className);
        if (typeof obj[methodName] !== 'function') {
            throw new Error(`Method ${methodName} does not exist on class ${className}`);
        }
        return obj[methodName](...args);
    }
}

module.exports = InteropApi;