var LoginService = function ($http, $q) {
  this.logins = function () {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }

    $http.get('/api/testlogins').
        success(function (data) {
          results.data = data;
          deferredObject.resolve(results);
        }).
        error(function (data, status, headers, config) {
          results.isSuccess = false;
          results.message = 'Could not get logins:';
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

  this.login = function (userName, password) {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }
    var itemData = {
      UserName: userName,
      Password: password
    };

    $http.post('/api/login', itemData).
        success(function (data) {
          results.data = data;
          deferredObject.resolve(results);
        }).
        error(function (data, status, headers, config) {
          results.isSuccess = false;
          results.message = 'Could not log in:';
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

  this.logout = function () {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }
    $http.delete('/api/login').
        success(function (data) {
          results.data = data;
          deferredObject.resolve(results);
        }).
        error(function (data, status, headers, config) {
          results.isSuccess = false;
          results.message = 'Could not log out:';
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

  this.register = function (userName, password, id, displayName, isActive) {
    var deferredObject = $q.defer();
    var results = {
      isSuccess: true,
      message: '',
      data: null
    }
    var itemData = {
      UserName: userName,
      Password: password,
      Id: id,
      DisplayName: displayName,
      IsActive: isActive
    };

    $http.post('/api/signup', itemData).
          success(function (data) {
            results.data = data;
            deferredObject.resolve(results);
          }).
          error(function (data, status, headers, config) {
            results.isSuccess = false;
            results.message = 'Could not register:';
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

LoginService.$inject = ['$http', '$q'];