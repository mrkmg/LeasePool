LeasePool - V0.2.0

A simple, configurable, thread-safe Object Pool. Provides a mechanism for constructing, validating, and disposing of objects on the fly, as well limiting the maximum number of total instaiated object and auto-disposal of stale objects.

Adheres to netstandard2.1

### Usage

```c#
using LeasePool;

ILeasePool<Connection> pool = new LeasePool<Connection>(
    new LeasePoolConfig<Connection>()
    {
        // Allow a maximum of 10 connections to be open at once.
        // Default: -1 (no limit)
        MaxLeases = 10,
        // Automatically dispose of stale connections after 30 seconds.
        // Default: -1 (no timeout)
        IdleTimeout = TimeSpan.FromSeconds(30),
        // Function to construct a new connection.
        // Default: Create new instance with Activator.CreateInstance<T>()
        Initializer = () => { 
            var connection = new Connection("hostname", "username", "password");
            connection.Open();
            return connection;
        },
        // Ensure that the connection is valid before leasing.
        // Default: (connection) => true
        Validator = (connection) => connection.IsConnected(),
        // Clear the history of the connection when returned to the pool.
        // Default: Do nothing
        OnReturn = (connection) => connection.ClearHistory(),
        // Not actually needed, as the default Finalizer will
        // Call Dispose if T is IDisposable. Just for demo purposes.
        Finalizer = (connection) => connection.Dispose()
    }
);

// Get a connection from the pool, waiting up 
// to 2 seconds for one to become available.
using (var connection = await pool.Lease(TimeSpan.FromSeconds(2))) {
    // do something with connection
}
```

### License

This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or distribute this software, either in source code form or as a compiled binary, for any purpose, commercial or non-commercial, and by any means.

In jurisdictions that recognize copyright laws, the author or authors of this software dedicate any and all copyright interest in the software to the public domain. We make this dedication for the benefit of the public at large and to the detriment of our heirs and successors. We intend this dedication to be an overt act of relinquishment in perpetuity of all present and future rights to this software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. For more information, please refer to <https://unlicense.org/>
 

