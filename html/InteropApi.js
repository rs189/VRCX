const dotnet = require('node-api-dotnet');
require('../bin/x64/Debug/VRCX.cjs');

class InteropApi {
    constructor() {
        this.createdObjects = {}; // Cache for .NET objects
    }

    getDotNetObject(className) {
        if (!this.createdObjects[className]) {
            console.log("[Node] Creating new .NET object: " + className);
            this.createdObjects[className] = new dotnet.VRCX[className]();
            console.log("[Node] Created new .NET object: " + className);
        }
        // Log created objects
        console.log("[Node] Created objects: ", this.createdObjects);
        console.log(this.createdObjects);

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
