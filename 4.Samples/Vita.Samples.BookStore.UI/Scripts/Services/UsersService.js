/*<copyright>
Should a copyright go here?
</copyright>*/

//------------------------------------------------------------------------------
/// <summary>This service enables CRUD operations on the data store for
/// IUser items via Web Api calls.</summary>
///
/// This file is code generated and should not be modified by hand.
/// If you need to customize outside of protected areas, add those changes
/// in another partial class file.  As a last resort (if generated code needs
/// to be different), change the Status value below to something other than
/// Generated to prevent changes from being overwritten.
///
/// <CreatedByUserName>INCODE-1\Dave</CreatedByUserName>
/// <CreatedDate>2/26/2015</CreatedDate>
/// <Status>Generated</Status>
//------------------------------------------------------------------------------
var UsersService = function ($http, $q) {
    this.searchUsers = function (orderBy, decending, page, pageSize) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            items: null,
            totalItems: 0,
            canCreate: true
        }
        var searchQuery = {
            OrderBy: orderBy,
            Descending: decending,
            Skip: (page - 1) * pageSize,
            Take: pageSize
        };
        if (searchQuery.Skip < 0) searchQuery.Skip = 0;
        
        $http.get('/api/users', { params: searchQuery }).
            success(function (data) {
                results.items = data.Items;
                results.totalItems = data.TotalItems;
                results.canCreate = data.CanCreate;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not search for User items: ';
                if (typeof data == "string") {
                    results.message = results.message + ' ' + data;
                } else {
                    for (var i = 0; i < data.length; i++) {
                        results.message = results.message + ' ' + data[i].Message;
                    }
                }
                deferredObject.resolve(results);
            });
        
        return deferredObject.promise;
    };
    
    this.getUser = function (id) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        
        $http.get('/api/users/' + id).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not get User item:';
                if (typeof data == "string") {
                    results.message = results.message + ' ' + data;
                } else {
                    for (var i = 0; i < data.length; i++) {
                        results.message = results.message + ' ' + data[i].Message;
                    }
                }
                deferredObject.resolve(results);
            });
        
        return deferredObject.promise;
    };
    
    this.listUser = function (id) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        
        $http.get('/api/users/list', { params: { take: 100, id: id } }).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not get User list:';
                if (typeof data == "string") {
                    results.message = results.message + ' ' + data;
                } else {
                    for (var i = 0; i < data.length; i++) {
                        results.message = results.message + ' ' + data[i].Message;
                    }
                }
                deferredObject.resolve(results);
            });
        
        return deferredObject.promise;
    };
    
    this.createUser = function (userName, userNameHash, displayName, isActive) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        var itemData = {
            UserName: userName, 
            UserNameHash: userNameHash, 
            DisplayName: displayName, 
            IsActive: isActive
        };
        
        $http.post('/api/users', itemData).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not create User item:';
                if (typeof data == "string") {
                    results.message = results.message + ' ' + data;
                } else {
                    for (var i = 0; i < data.length; i++) {
                        results.message = results.message + ' ' + data[i].Message;
                    }
                }
                deferredObject.resolve(results);
            });
        
        return deferredObject.promise;
    };
    
    this.updateUser = function (id, userName, userNameHash, displayName, isActive) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        var itemData = {
            Id: id, 
            UserName: userName, 
            UserNameHash: userNameHash, 
            DisplayName: displayName, 
            IsActive: isActive
        };
        
        $http.put('/api/users', itemData).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not update User item:';
                if (typeof data == "string") {
                    results.message = results.message + ' ' + data;
                } else {
                    for (var i = 0; i < data.length; i++) {
                        results.message = results.message + ' ' + data[i].Message;
                    }
                }
                deferredObject.resolve(results);
            });
        
        return deferredObject.promise;
    };
    
    this.deleteUser = function (id) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        
        $http.delete('/api/users/' + id).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not delete User item:';
                if (typeof data == "string") {
                    results.message = results.message + ' ' + data;
                } else {
                    for (var i = 0; i < data.length; i++) {
                        results.message = results.message + ' ' + data[i].Message;
                    }
                }
                deferredObject.resolve(results);
            });
        
        return deferredObject.promise;
    };
}

UsersService.$inject = ['$http', '$q'];