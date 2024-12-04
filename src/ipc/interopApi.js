class InteropApi {
    constructor() {
    	// Proxy to dynamically create .NET class wrappers
    	return new Proxy(this, {
    		get(target, prop) {
    			// If the property is not a method of InteropApi, 
    			// treat it as a .NET class name
    			if (typeof prop === 'string' && !target[prop]) {
    				return new Proxy({}, {
    					get(_, methodName) {
    						// Return a method that calls the .NET method dynamically
    						return async (...args) => {
    							console.trace();
    							console.log(`Calling ${prop}.${methodName} with args:`, args);
    							return await target.callMethod(prop, methodName, ...args);
    						};
    					}
    				});
    			}
    			// Return the original property if it exists
    			return target[prop];
    		}
    	});
    }
  
    async callMethod(className, methodName, ...args) {
    	console.trace();
		console.log(`Calling ${className}.${methodName} with args:`, args);
    	return window.interopApi.callDotNetMethod(className, methodName, args)
    		.then(result => {
    			console.log(`Result from ${className}.${methodName}:`, result);
    			return result;
    	});
    }
}

export default new InteropApi();