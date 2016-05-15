//------------------------------------------------------------------------------
/// <summary>This service enables CRUD operations on the data store for
/// IBookOrder items via Web Api calls.</summary>
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
var OrdersService = function ($http, $q) {
  this.addToCart = function (bookId) {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }

    var itemData = {
      Book: { Id: bookId },
      Quantity: 1
    }

    $http.post('/api/user/cart/item', itemData).
          success(function (data) {
            results.data = data;
            deferredObject.resolve(results);
          }).
          error(function (data, status, headers, config) {
            results.isSuccess = false;
            results.message = 'Could not get cart:';
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

  this.saveCart = function (items) {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }
    for(var i=0; i<items.length; i++) {
      items[i].Quantity = items[i].Quantity || 0;
    }
    var itemData = {
      Items: items
    }

    $http.put('/api/user/cart', itemData).
          success(function (data) {
            results.data = data;
            deferredObject.resolve(results);
          }).
          error(function (data, status, headers, config) {
            results.isSuccess = false;
            results.message = 'Could not save cart:';
            if (status == 400) { //Bad request, server returns array of error objects
              for (var i = 0; i < data.length; i++) {
                results.message = results.message + ' ' + data[i].Message;
              }
            } else if (typeof data == "string") {
              results.message = results.message + ' ' + data;
            } else {
              results.message = results.message + ' Server error.';
            }
            deferredObject.resolve(results);
          });

    return deferredObject.promise;
  };

  this.getCart = function () {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }

    $http.get('/api/user/cart').
          success(function (data) {
            results.data = data;
            deferredObject.resolve(results);
          }).
          error(function (data, status, headers, config) {
            results.isSuccess = false;
            results.message = 'Could not get cart:';
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


  this.placeOrder = function (coupon) {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }

    var itemData = {
      Coupon: coupon
    }

    $http.put('/api/user/cart/submit', itemData).
          success(function (data) {
            results.data = data;
            deferredObject.resolve(results);
          }).
          error(function (data, status, headers, config) {
            results.isSuccess = false;
            results.message = 'Could not place order:';
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

  this.getOrders = function () {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      items: null,
      totalItems: 0,
      canCreate: true
    }

    $http.get('/api/user/orders').
        success(function (data) {
          results.items = data;
          deferredObject.resolve(results);
        }).
        error(function (data, status, headers, config) {
          results.isSuccess = false;
          results.message = 'Could not get orders: ';
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

  this.getOrder = function (orderId) {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }

    $http.get('/api/user/orders/' + orderId).
          success(function (data) {
            results.data = data;
            deferredObject.resolve(results);
          }).
          error(function (data, status, headers, config) {
            results.isSuccess = false;
            results.message = 'Could not get BookOrder item:';
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

OrdersService.$inject = ['$http', '$q'];