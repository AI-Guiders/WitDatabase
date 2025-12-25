/**
 * WitDatabase IndexedDB Index Helper Library
 * Provides key-value operations for secondary indexes.
 * 
 * @version 1.0.0
 */

(() => {
    'use strict';

    const DB_VERSION_BASE = 1;

    // Connection and version cache
    const connectionCache = new Map();
    const storeVersions = new Map();

    /**
     * Gets the current version for a database (based on stores).
     */
    function getDbVersion(databaseName) {
        return storeVersions.get(databaseName) || DB_VERSION_BASE;
    }

    /**
     * Opens or creates an IndexedDB database with the specified store.
     * @param {string} databaseName - Name of the database
     * @param {string} storeName - Name of the object store
     * @returns {Promise<IDBDatabase>}
     */
    async function openDatabase(databaseName, storeName) {
        // Check if we have an open connection with this store
        const cacheKey = databaseName;
        const cached = connectionCache.get(cacheKey);
        
        if (cached && cached.db && !cached.db.closed) {
            // Check if store exists
            if (cached.db.objectStoreNames.contains(storeName)) {
                return cached.db;
            }
            // Need to upgrade to add new store
            cached.db.close();
            connectionCache.delete(cacheKey);
        }

        // Get current version and increment if needed
        let version = getDbVersion(databaseName);
        
        // Try to open with current version first
        let db = await tryOpenDatabase(databaseName, version);
        
        // If store doesn't exist, upgrade
        if (!db.objectStoreNames.contains(storeName)) {
            db.close();
            version++;
            storeVersions.set(databaseName, version);
            db = await openWithUpgrade(databaseName, version, storeName);
        }

        connectionCache.set(cacheKey, { db });
        return db;
    }

    /**
     * Try to open database without upgrade.
     */
    function tryOpenDatabase(databaseName, version) {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(databaseName, version);
            
            request.onerror = () => {
                reject(new Error(`Failed to open database '${databaseName}': ${request.error?.message}`));
            };
            
            request.onsuccess = () => {
                resolve(request.result);
            };
            
            request.onupgradeneeded = () => {
                // Just let it complete, we'll check stores after
            };
        });
    }

    /**
     * Open database and ensure store exists.
     */
    function openWithUpgrade(databaseName, version, storeName) {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(databaseName, version);
            
            request.onerror = () => {
                reject(new Error(`Failed to open database '${databaseName}': ${request.error?.message}`));
            };
            
            request.onsuccess = () => {
                resolve(request.result);
            };
            
            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(storeName)) {
                    db.createObjectStore(storeName);
                }
            };
        });
    }

    /**
     * Closes database connection.
     * @param {string} databaseName - Name of the database
     */
    function closeDatabase(databaseName) {
        const cached = connectionCache.get(databaseName);
        if (cached && cached.db) {
            cached.db.close();
            connectionCache.delete(databaseName);
        }
    }

    /**
     * Compares two Uint8Arrays lexicographically.
     * @returns negative if a < b, 0 if equal, positive if a > b
     */
    function compareBytes(a, b) {
        if (!a && !b) return 0;
        if (!a) return -1;
        if (!b) return 1;
        
        const minLen = Math.min(a.length, b.length);
        for (let i = 0; i < minLen; i++) {
            if (a[i] !== b[i]) {
                return a[i] - b[i];
            }
        }
        return a.length - b.length;
    }

    /**
     * Converts byte array to base64 for use as IndexedDB key.
     */
    function bytesToKey(bytes) {
        if (!bytes || bytes.length === 0) return '';
        // Use a simple string representation for IndexedDB key
        return Array.from(bytes).map(b => String.fromCharCode(b)).join('');
    }

    /**
     * Converts key back to byte array.
     */
    function keyToBytes(key) {
        if (!key || key.length === 0) return new Uint8Array(0);
        return new Uint8Array(Array.from(key).map(c => c.charCodeAt(0)));
    }

    /**
     * Gets a value by key.
     */
    async function get(databaseName, storeName, key) {
        const db = await openDatabase(databaseName, storeName);
        const strKey = bytesToKey(key);

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readonly');
            const store = transaction.objectStore(storeName);
            const request = store.get(strKey);

            request.onerror = () => {
                reject(new Error(`Failed to get key: ${request.error?.message}`));
            };

            request.onsuccess = () => {
                const result = request.result;
                if (result !== undefined) {
                    if (result instanceof Uint8Array) {
                        resolve(result);
                    } else if (result instanceof ArrayBuffer) {
                        resolve(new Uint8Array(result));
                    } else if (Array.isArray(result)) {
                        resolve(new Uint8Array(result));
                    } else {
                        resolve(result);
                    }
                } else {
                    resolve(null);
                }
            };
        });
    }

    /**
     * Puts a key-value pair.
     */
    async function put(databaseName, storeName, key, value) {
        const db = await openDatabase(databaseName, storeName);
        const strKey = bytesToKey(key);
        const storeValue = value instanceof Uint8Array ? value : new Uint8Array(value || []);

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readwrite');
            const store = transaction.objectStore(storeName);
            const request = store.put(storeValue, strKey);

            request.onerror = () => {
                reject(new Error(`Failed to put key: ${request.error?.message}`));
            };

            transaction.oncomplete = () => {
                resolve();
            };
        });
    }

    /**
     * Deletes a key.
     */
    async function deleteKey(databaseName, storeName, key) {
        const db = await openDatabase(databaseName, storeName);
        const strKey = bytesToKey(key);

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readwrite');
            const store = transaction.objectStore(storeName);
            
            // First check if key exists
            const getRequest = store.get(strKey);
            
            getRequest.onsuccess = () => {
                if (getRequest.result === undefined) {
                    resolve(false);
                    return;
                }
                
                const deleteRequest = store.delete(strKey);
                deleteRequest.onerror = () => {
                    reject(new Error(`Failed to delete key: ${deleteRequest.error?.message}`));
                };
            };

            transaction.oncomplete = () => {
                if (getRequest.result !== undefined) {
                    resolve(true);
                }
            };

            transaction.onerror = () => {
                reject(new Error(`Transaction failed: ${transaction.error?.message}`));
            };
        });
    }

    /**
     * Deletes all keys in a range.
     */
    async function deleteRange(databaseName, storeName, startKey, endKey) {
        const db = await openDatabase(databaseName, storeName);
        const startBytes = startKey ? new Uint8Array(startKey) : null;
        const endBytes = endKey ? new Uint8Array(endKey) : null;

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readwrite');
            const store = transaction.objectStore(storeName);
            const keysToDelete = [];
            const request = store.openCursor();

            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor) {
                    const keyBytes = keyToBytes(cursor.key);
                    
                    // Check if key is in range
                    const afterStart = !startBytes || compareBytes(keyBytes, startBytes) >= 0;
                    const beforeEnd = !endBytes || compareBytes(keyBytes, endBytes) < 0;
                    
                    if (afterStart && beforeEnd) {
                        keysToDelete.push(cursor.key);
                    }
                    
                    cursor.continue();
                } else {
                    // Delete all collected keys
                    for (const key of keysToDelete) {
                        store.delete(key);
                    }
                }
            };

            transaction.oncomplete = () => {
                resolve(keysToDelete.length);
            };

            transaction.onerror = () => {
                reject(new Error(`Delete range failed: ${transaction.error?.message}`));
            };
        });
    }

    /**
     * Scans key-value pairs in a range.
     */
    async function scan(databaseName, storeName, startKey, endKey) {
        const db = await openDatabase(databaseName, storeName);
        const startBytes = startKey ? new Uint8Array(startKey) : null;
        const endBytes = endKey ? new Uint8Array(endKey) : null;

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readonly');
            const store = transaction.objectStore(storeName);
            const results = [];
            const request = store.openCursor();

            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor) {
                    const keyBytes = keyToBytes(cursor.key);
                    
                    // Check if key is in range
                    const afterStart = !startBytes || compareBytes(keyBytes, startBytes) >= 0;
                    const beforeEnd = !endBytes || compareBytes(keyBytes, endBytes) < 0;
                    
                    if (afterStart && beforeEnd) {
                        const value = cursor.value instanceof Uint8Array 
                            ? cursor.value 
                            : new Uint8Array(cursor.value || []);
                        results.push({ 
                            key: keyBytes, 
                            value: value 
                        });
                    }
                    
                    cursor.continue();
                }
            };

            transaction.oncomplete = () => {
                // Sort results by key
                results.sort((a, b) => compareBytes(a.key, b.key));
                resolve(results);
            };

            transaction.onerror = () => {
                reject(new Error(`Scan failed: ${transaction.error?.message}`));
            };
        });
    }

    /**
     * Checks if any keys exist in a range.
     */
    async function hasAny(databaseName, storeName, startKey, endKey) {
        const db = await openDatabase(databaseName, storeName);
        const startBytes = startKey ? new Uint8Array(startKey) : null;
        const endBytes = endKey ? new Uint8Array(endKey) : null;

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readonly');
            const store = transaction.objectStore(storeName);
            const request = store.openCursor();
            let found = false;

            request.onsuccess = (event) => {
                const cursor = event.target.result;
                if (cursor && !found) {
                    const keyBytes = keyToBytes(cursor.key);
                    
                    const afterStart = !startBytes || compareBytes(keyBytes, startBytes) >= 0;
                    const beforeEnd = !endBytes || compareBytes(keyBytes, endBytes) < 0;
                    
                    if (afterStart && beforeEnd) {
                        found = true;
                        resolve(true);
                        return;
                    }
                    
                    cursor.continue();
                } else if (!found) {
                    resolve(false);
                }
            };

            transaction.onerror = () => {
                reject(new Error(`HasAny failed: ${transaction.error?.message}`));
            };
        });
    }

    /**
     * Gets the count of entries in the store.
     */
    async function count(databaseName, storeName) {
        const db = await openDatabase(databaseName, storeName);

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readonly');
            const store = transaction.objectStore(storeName);
            const request = store.count();

            request.onerror = () => {
                reject(new Error(`Count failed: ${request.error?.message}`));
            };

            request.onsuccess = () => {
                resolve(request.result);
            };
        });
    }

    /**
     * Clears all entries from the store.
     */
    async function clear(databaseName, storeName) {
        const db = await openDatabase(databaseName, storeName);

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, 'readwrite');
            const store = transaction.objectStore(storeName);
            const request = store.clear();

            request.onerror = () => {
                reject(new Error(`Clear failed: ${request.error?.message}`));
            };

            transaction.oncomplete = () => {
                resolve();
            };
        });
    }

    // Expose functions to global scope for Blazor interop
    window.witDbIndex = {
        open: openDatabase,
        close: closeDatabase,
        get: get,
        put: put,
        delete: deleteKey,
        deleteRange: deleteRange,
        scan: scan,
        hasAny: hasAny,
        count: count,
        clear: clear
    };

})();
