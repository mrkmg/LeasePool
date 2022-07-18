LeasePool - V0.8.0

A simple, configurable, thread-safe Object Pool. Provides a mechanism for constructing, validating, and disposing of objects on the fly, as well limiting the maximum number of total instantiated object and auto-disposal of stale objects.

Adheres to netstandard2.1

### Usage

```c#
using LeasePool;

ILeasePool<Connection> pool = new LeasePool<Connection>(
    new LeasePoolConfiguration<Connection>()
    {
        // Maximum number of leases in the pool
        MaxLeases = 10, 
        
        // Maximum time (in milliseconds) an object can remain idle before it is disposed
        IdleTimeout = 30000, 
        
        // How to create and initialize a new object
        Initializer = () => { 
            var connection = new Connection("hostname", "username", "password");
            connection.Open();
            return connection;
        },
        
        // How to finalize an object
        Finalizer = (connection) => connection.Dispose()
        
        // How to validate an object is okay to lease out
        Validator = (connection) => connection.IsConnected(),
        
        // Called before object is leased
        OnLease = (connection) => connection.StartTransaction(),
        
        // Called after object is returned
        OnReturn = (connection) => connection.EndTransaction(),
    }
);

// Get a connection from the pool, waiting up 
// to 2 seconds for one to become available.
using (var connection = await pool.LeaseAsync(TimeSpan.FromSeconds(2))) {
    // do something with connection
}
```

### Configuration Options

- **MaxLeases** *int* The maximum number of object which can be instantiated at once. Default: -1 (no limit) 
- **IdleTimeout** *int* The maximum amount of time an object can remain idle before it is automatically disposed. Default: -1 (no timeout)
- **Initializer** *Func&lt;T&gt;* A function to create a new instance. Default: `() => Activator.CreateInstance<T>()`
- **Validator** *Func&lt;T, bool&gt;* A function to validate an instance. If this returns false, the object will be disposed. Default: `(instance) => true`
- **OnLease** *Action&lt;T&gt;* A function to execute before an instance is leased. Default: Do nothing
- **OnReturn** *Action&lt;T&gt;* A function to execute after an instance is returned. Default: Do nothing
- **Finalizer** *Action&lt;T&gt;* A function to execute when an instance is disposed. Default: If the object is an `IDisposable`, call `Dispose()` on it.

### License

This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or distribute this software, either in source code form or as a compiled binary, for any purpose, commercial or non-commercial, and by any means.

In jurisdictions that recognize copyright laws, the author or authors of this software dedicate any and all copyright interest in the software to the public domain. We make this dedication for the benefit of the public at large and to the detriment of our heirs and successors. We intend this dedication to be an overt act of relinquishment in perpetuity of all present and future rights to this software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. For more information, please refer to <https://unlicense.org/>
