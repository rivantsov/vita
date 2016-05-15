/*<copyright>
Should a copyright go here?
</copyright>*/

//------------------------------------------------------------------------------
/// <summary>This service enables CRUD operations on the data store for
/// IBook items via Web Api calls.</summary>
//------------------------------------------------------------------------------
var BooksService = function ($http, $q) {
    this.searchBooks = function (title, categories, maxPrice, publisher, publishedAfter, publishedBefore, authorLastName, orderBy, descending, page, pageSize) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            items: null,
            totalItems: 0,
            canCreate: true
        }
        var searchQuery = {
            Title: title,
            Categories: categories,
            MaxPrice: maxPrice,
            Publisher: publisher,
            PublishedAfter: publishedAfter,
            PublishedBefore: publishedBefore,
            AuthorLastName: authorLastName,
            OrderBy: orderBy,
            Skip: (page - 1) * pageSize,
            Take: pageSize
        };
        if (searchQuery.Skip < 0) searchQuery.Skip = 0;
        if (orderBy != '' && descending == true) {
          searchQuery.OrderBy = orderBy + "-desc";
        }
        
        $http.get('/api/books', { params: searchQuery }).
            success(function (data) {
                results.items = data.Results;
                results.totalItems = data.TotalCount;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not search for Book items: ';
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
    
    this.getBook = function (id) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        
        $http.get('/api/books/' + id).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not get Book item:';
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
    

    this.getImage = function (id) {
      var deferredObject = $q.defer();
      var results = {
        isSuccess: true,
        message: '',
        data: null
      }

      $http.get('/api/images/' + id).
          success(function (data) {
            results.data = data;
            deferredObject.resolve(results);
          }).
          error(function (data, status, headers, config) {
            results.isSuccess = false;
            results.message = 'Could not get image:';
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

    this.listBook = function (id) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        
        $http.get('/api/books/list', { params: { take: 100, id: id } }).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not get Book list:';
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
    
    this.createBook = function (title, description, publishedOn, abstract, category, editions, price, publisherId) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        var itemData = {
            Title: title, 
            Description: description, 
            PublishedOn: publishedOn, 
            Abstract: abstract, 
            Category: category, 
            Editions: editions, 
            Price: price, 
            PublisherId: publisherId
        };
        
        $http.post('/api/books', itemData).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not create Book item:';
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
    
    this.updateBook = function (id, title, description, publishedOn, abstract, category, editions, price, publisherId) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        var itemData = {
            Id: id, 
            Title: title, 
            Description: description, 
            PublishedOn: publishedOn, 
            Abstract: abstract, 
            Category: category, 
            Editions: editions, 
            Price: price, 
            PublisherId: publisherId
        };
        
        $http.put('/api/books', itemData).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not update Book item:';
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
    
    this.deleteBook = function (id) {
        var deferredObject = $q.defer();
        var results = {
            isSuccess: true,
            message: '',
            data: null
        }
        
        $http.delete('/api/books/' + id).
            success(function (data) {
                results.data = data;
                deferredObject.resolve(results);
            }).
            error(function (data, status, headers, config) {
                results.isSuccess = false;
                results.message = 'Could not delete Book item:';
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

BooksService.$inject = ['$http', '$q'];